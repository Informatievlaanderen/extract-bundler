namespace ExtractBundler.Console.Bundlers;

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CloudStorageClients;
using HttpClients;
using Infrastructure.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public abstract class BaseBundler : IDisposable
{
    private readonly MetaDataCenterHttpClient _metadataClient;
    private readonly S3Client _s3Client;
    private readonly AzureBlobClient _azureBlobClient;
    private readonly AzureBlobOptions _azureOptions;
    private readonly BundlerOptionItem _bundlerOption;

    private readonly MemoryStream _s3ZipArchiveStream;
    private readonly ZipArchive _s3ZipArchive;

    private readonly MemoryStream _azureZipArchiveStream;
    private readonly ZipArchive _azureZipArchive;

    private bool _disposed = false;
    private readonly ILogger<BaseBundler> _logger;
    private readonly ExtractDownloader _extractDownloader;

    protected BaseBundler(
        MetaDataCenterHttpClient metadataClient,
        S3Client s3Client,
        AzureBlobClient azureBlobClient,
        ILoggerFactory loggerFactory,
        IOptions<AzureBlobOptions> azureOptions,
        BundlerOptionItem bundlerOption
    )
    {
        _bundlerOption = bundlerOption;
        _extractDownloader = new ExtractDownloader(loggerFactory);
        _logger = loggerFactory.CreateLogger<BaseBundler>();
        _metadataClient = metadataClient;
        _s3Client = s3Client;
        _azureBlobClient = azureBlobClient;
        _azureOptions = azureOptions.Value;

        _s3ZipArchiveStream = new MemoryStream();
        _s3ZipArchive = new ZipArchive(_s3ZipArchiveStream, ZipArchiveMode.Create, true);

        _azureZipArchiveStream = new MemoryStream();
        _azureZipArchive = new ZipArchive(_azureZipArchiveStream, ZipArchiveMode.Create, true);
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        if (!_bundlerOption.Enabled)
        {
            return;
        }

        await _extractDownloader.DownloadAllAsync(
            _bundlerOption.UrlsToList(),
            AddToS3ZipArchiveAsync,
            cancellationToken);
        _extractDownloader.Dispose();
        _s3ZipArchive.Dispose();
        await _s3Client.UploadBlobInChunksAsync(_s3ZipArchiveStream, GetIdentifier(), isGeoPackage: false,
            cancellationToken);
        _logger.LogWarning("Upload to S3 Blob completed.");

        if (!_azureOptions.Enabled)
        {
            await _s3ZipArchiveStream.DisposeAsync();
            _disposed = true;
        }
        else
        {
            _s3ZipArchiveStream.Seek(0, SeekOrigin.Begin);
            await AddToAzureZipArchiveAsync(_s3ZipArchiveStream, cancellationToken).ConfigureAwait(false);
            await AddAdditionalFilesToAzureZipArchiveAsync(cancellationToken).ConfigureAwait(false);
            await _s3ZipArchiveStream.DisposeAsync();
            _azureZipArchive.Dispose();

            await _azureBlobClient.UploadBlobInChunksAsync(
                _azureZipArchiveStream,
                GetIdentifier(),
                isGeoPackage: false,
                cancellationToken);

            if (_bundlerOption.GeopackageEnabled)
            {
                await CreateGeoPackage(cancellationToken);
            }

            await _azureZipArchiveStream.DisposeAsync();

            _logger.LogWarning("Upload to Azure Blob completed.");
            _logger.LogWarning(GetIdentifier().GetValue(ZipKey.ExtractDoneMessage));
            _disposed = true;
        }
    }

    private async Task CreateGeoPackage(CancellationToken cancellationToken)
    {
        try
        {
            var workDir = "geopackages";
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, true);

            var currentDir = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(workDir);
            _azureZipArchiveStream.Seek(0, SeekOrigin.Begin);
            var downloadShapeZip = Path.Combine(workDir, $"{GetIdentifier().GetValue(ZipKey.AzureZip)}");
            await using var fileStream = new FileStream(downloadShapeZip, FileMode.Create, FileAccess.Write);
            await _azureZipArchiveStream.CopyToAsync(fileStream, cancellationToken);
            fileStream.Close();
            var extractPath = Path.Combine(workDir);
            ZipFile.ExtractToDirectory(downloadShapeZip, extractPath, true);
            File.Delete(downloadShapeZip);

            var dataDirs = Directory.GetDirectories(workDir, "*_GRAR_*_Data", SearchOption.TopDirectoryOnly);
            if (dataDirs.Length == 0)
                throw new InvalidOperationException("No *_GRAR_*_Data directory found.");

            if (dataDirs.Length > 1)
                throw new InvalidOperationException(
                    "Multiple *_GRAR_*_Data directories found; handle selection explicitly.");

            string dataRoot = dataDirs[0];

            // 2) Shapefile + metadata paths
            string shapeFilesDir = Path.Combine(dataRoot, "Shapefile");
            //find all files with .shp
            var shapeFiles = Path.Exists(shapeFilesDir)
                ? Directory.GetFiles(shapeFilesDir, "*.shp", SearchOption.TopDirectoryOnly)
                : [];
            //find all files with _metdata.dbf
            var shapeMetaFiles = Path.Exists(shapeFilesDir)
                ? Directory.GetFiles(shapeFilesDir, "*_metadata.dbf", SearchOption.TopDirectoryOnly)
                : [];
            //all dBASE files
            var dbaseFilesDir = Path.Combine(dataRoot, "dBASE");
            var dbaseFiles = Path.Exists(dbaseFilesDir)
                ? Directory.GetFiles(dbaseFilesDir, "*.dbf", SearchOption.TopDirectoryOnly)
                : [];

            string geopkgDir = Path.Combine(dataRoot, "Geopackage");
            Directory.CreateDirectory(geopkgDir);
            string outGpkg = Path.GetFullPath(Path.Combine(currentDir, geopkgDir, GetIdentifier().GetValue(ZipKey.GeoPackage)));

            _logger.LogWarning($"Output GeoPackage path: {outGpkg}");

            foreach (var shapeFile in shapeFiles)
            {
                var layerName = Path.GetFileNameWithoutExtension(shapeFile);
                var absoluteShapeFile = Path.GetFullPath(Path.Combine(currentDir, shapeFile));
                if (shapeFile != shapeFiles.First())
                {
                    await RunOgr2OgrAsync(
                        // -update: open existing gpkg
                        "ogr2ogr -f GPKG -update -append " +
                        $"\"{outGpkg}\" \"{absoluteShapeFile}\" " +
                        $"-nln \"{layerName}\" -nlt PROMOTE_TO_MULTI " +
                        "-lco SPATIAL_INDEX=YES -dsco WRITE_BBOX=YES -oo ENCODING=UTF-8",
                        shapeFilesDir,
                        cancellationToken);
                }
                else
                {
                    await RunOgr2OgrAsync(
                        // first call creates the geopackage
                        $"ogr2ogr -f GPKG \"{outGpkg}\" \"{absoluteShapeFile}\" " +
                        $"-nln \"{layerName}\" -nlt PROMOTE_TO_MULTI " +
                        "-lco SPATIAL_INDEX=YES -dsco WRITE_BBOX=YES -oo ENCODING=UTF-8",
                        shapeFilesDir,
                        cancellationToken);
                }
            }

            foreach (var metaFile in shapeMetaFiles)
            {
                var layerName = Path.GetFileNameWithoutExtension(metaFile);
                var absoluteMetaFile = Path.GetFullPath(Path.Combine(currentDir, metaFile));
                // 5) Append metadata DBF as a non-spatial table in the same GPKG
                await RunOgr2OgrAsync(
                    // -update: open existing gpkg
                    // -nlt NONE: non-spatial table
                    "ogr2ogr -f GPKG -update -append " +
                    $"\"{outGpkg}\" \"{absoluteMetaFile}\" " +
                    $"-nln \"{layerName}\" -nlt NONE",
                    shapeFilesDir,
                    cancellationToken
                );
            }

            foreach (var dbaseFile in dbaseFiles)
            {
                var layerName = Path.GetFileNameWithoutExtension(dbaseFile);
                var absoluteDbaseFile = Path.GetFullPath(Path.Combine(currentDir, dbaseFile));
                _logger.LogWarning($"Attempting to process: {absoluteDbaseFile}");
                _logger.LogWarning($"File exists: {File.Exists(absoluteDbaseFile)}");
                if (shapeFiles.Any() || dbaseFile != dbaseFiles.First())
                {
                    // 5) Append metadata DBF as a non-spatial table in the same GPKG
                    await RunOgr2OgrAsync(
                        // -update: open existing gpkg
                        // -nlt NONE: non-spatial table
                        "ogr2ogr -f GPKG -update -append " +
                        $"\"{outGpkg}\" \"{absoluteDbaseFile}\" " +
                        $"-nln \"{layerName}\" -nlt NONE",
                        dbaseFilesDir,
                        cancellationToken
                    );
                }
                else
                {
                    // No shapefiles found, so create the geopackage from the dBASE files
                    await RunOgr2OgrAsync(
                        // first call creates the geopackage
                        $"ogr2ogr -f GPKG \"{outGpkg}\" \"{absoluteDbaseFile}\" " +
                        $"-nln \"{layerName}\" -nlt NONE",
                        dbaseFilesDir,
                        cancellationToken);
                }
            }

            if (Directory.Exists(Path.Combine(workDir, dataRoot, "Shapefile")))
            {
                _logger.LogWarning("Removing Shapefile directory");
                Directory.Delete(Path.Combine(workDir, dataRoot, "Shapefile"), true);
            }

            if (Directory.Exists(Path.Combine(workDir, dataRoot, "dBASE")))
            {
                _logger.LogWarning("Removing dBASE directory");
                Directory.Delete(Path.Combine(workDir, dataRoot, "dBASE"), true);
            }

            //create new zip and upload to azure if enabled, otherwise s3
            await using var geopackageZipStream = new MemoryStream();
            using var geopackageZip = new ZipArchive(geopackageZipStream, ZipArchiveMode.Create, true);
            //zip all files and directories in the workDir with same folder structure
            foreach (var filePath in Directory.GetFiles(workDir, "*", SearchOption.AllDirectories))
            {
                var entryName = Path.GetRelativePath(workDir, filePath);
                geopackageZip.CreateEntryFromFile(filePath, entryName);
            }

            geopackageZip.Dispose();
            geopackageZipStream.Seek(0, SeekOrigin.Begin);

            await _azureBlobClient.UploadBlobInChunksAsync(
                geopackageZipStream,
                GetIdentifier(),
                isGeoPackage: true,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating geopackage.");
        }
    }

    private async Task AddToS3ZipArchiveAsync(Stream zipArchiveStream, CancellationToken cancellationToken = default)
    {
        using var zipArchive = new ZipArchive(zipArchiveStream, ZipArchiveMode.Read);
        foreach (var entry in zipArchive.Entries)
        {
            _logger.LogWarning($"[{GetIdentifier().GetValue(ZipKey.S3Zip)}] ADD {entry.FullName}");
            await entry.CopyToAsync(_s3ZipArchive, entry.FullName, cancellationToken);
        }
    }

    private async Task AddToAzureZipArchiveAsync(Stream zipArchiveStream, CancellationToken cancellationToken = default)
    {
        using var zipArchive = new ZipArchive(zipArchiveStream, ZipArchiveMode.Read);
        foreach (var entry in zipArchive.Entries)
        {
            var entryFileName = GetIdentifier().RewriteZipEntryFullNameForAzure(entry.FullName);
            _logger.LogWarning($"[{GetIdentifier().GetValue(ZipKey.AzureZip)}] ADD {entryFileName} ");
            await entry.CopyToAsync(_azureZipArchive, entryFileName, cancellationToken);
        }
    }

    private async Task AddAdditionalFilesToAzureZipArchiveAsync(CancellationToken cancellationToken)
    {
        //Update MetaDataCenter
        var results = await _metadataClient.UpdateCswPublication(
            GetIdentifier(),
            DateTime.Now,
            cancellationToken);
        if (results == null)
        {
            _logger.LogCritical(GetIdentifier().GetValue(ZipKey.MetadataUpdatedMessageFailed));
            return;
        }

        _logger.LogWarning(GetIdentifier().GetValue(ZipKey.MetadataUpdatedMessage));

        //Download MetaDataCenter files
        var pdfAsBytes = await _metadataClient.GetPdfAsByteArray(GetIdentifier(), cancellationToken);
        var xmlAsString = await _metadataClient.GetXmlAsString(GetIdentifier(), cancellationToken);

        _logger.LogWarning(
            $"[{GetIdentifier().GetValue(ZipKey.AzureZip)}] ADD {GetIdentifier().GetValue(ZipKey.MetaGrarXml)}");
        _logger.LogWarning(
            $"[{GetIdentifier().GetValue(ZipKey.AzureZip)}] ADD {GetIdentifier().GetValue(ZipKey.MetaGrarPdf)}");

        //Append to azure
        await _azureZipArchive.AddToZipArchive(
            GetIdentifier().GetValue(ZipKey.MetaGrarXml),
            xmlAsString,
            cancellationToken);

        await _azureZipArchive.AddToZipArchive(
            GetIdentifier().GetValue(ZipKey.MetaGrarPdf),
            pdfAsBytes,
            cancellationToken);

        var instructionPdfAsBytes = await File.ReadAllBytesAsync(
            Path.Join(AppDomain.CurrentDomain.BaseDirectory, GetIdentifier().GetValue(ZipKey.InstructionPdf)),
            cancellationToken);

        _logger.LogWarning(
            $"[{GetIdentifier().GetValue(ZipKey.AzureZip)}] ADD {GetIdentifier().GetValue(ZipKey.InstructionPdf)}");

        await _azureZipArchive.AddToZipArchive(
            GetIdentifier().GetValue(ZipKey.InstructionPdf),
            instructionPdfAsBytes,
            cancellationToken);
    }

    public static async Task RunOgr2OgrAsync(string command, string workingDir, CancellationToken ct)
    {
        // Parse command into executable and arguments
        var parts = command.Split(' ', 2);

        var psi = new ProcessStartInfo
        {
            FileName = "ogr2ogr",  // Direct call
            Arguments = parts.Length > 1 ? parts[1] : "",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            EnvironmentVariables = { ["GDAL_DATA"] = "/usr/share/gdal" }
        };

        using var p = Process.Start(psi)!;

        // Start reading both streams concurrently to avoid deadlocks.
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();

        // Wait for process exit and for both reads to complete.
        await Task.WhenAll(p.WaitForExitAsync(ct), stdoutTask, stderrTask).ConfigureAwait(false);

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (p.ExitCode != 0)
            throw new InvalidOperationException(
                $"ogr2ogr failed (exit {p.ExitCode}).\nCMD: {command}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");

        await Task.Delay(1000, ct); // Allow file handles to release
    }


    protected abstract Identifier GetIdentifier();

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources.
                _s3ZipArchive.Dispose();
                _azureZipArchive.Dispose();
                _s3ZipArchiveStream.Dispose();
                _azureZipArchiveStream.Dispose();
                _extractDownloader.Dispose();
            }

            _disposed = true;
        }
    }
}
