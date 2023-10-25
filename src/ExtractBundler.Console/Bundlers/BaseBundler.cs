namespace ExtractBundler.Console.Bundlers;

using System;
using System.IO;
using System.IO.Compression;
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
        ExtractDownloader extractDownloader,
        BundlerOptionItem bundlerOption
    )
    {
        _bundlerOption = bundlerOption;
        _extractDownloader = extractDownloader;
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
        await _s3Client.UploadBlobInChunksAsync(_s3ZipArchiveStream, GetIdentifier(), cancellationToken);
        _logger.LogWarning("Upload to S3 Blob completed.");

        if (!_azureOptions.Enabled)
        {
            await _s3ZipArchiveStream.DisposeAsync();
            _disposed = true;
            return;
        }

        _s3ZipArchiveStream.Seek(0, SeekOrigin.Begin);
        await AddToAzureZipArchiveAsync(_s3ZipArchiveStream, cancellationToken).ConfigureAwait(false);
        await AddAdditionalFilesToAzureZipArchiveAsync(cancellationToken).ConfigureAwait(false);
        await _s3ZipArchiveStream.DisposeAsync();
        _azureZipArchive.Dispose();

        await _azureBlobClient.UploadBlobInChunksAsync(
            _azureZipArchiveStream,
            GetIdentifier(),
            cancellationToken);
        await _azureZipArchiveStream.DisposeAsync();

        _logger.LogWarning("Upload to Azure Blob completed.");
        _logger.LogWarning(GetIdentifier().GetValue(ZipKey.ExtractDoneMessage));
        _disposed = true;
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
                _s3ZipArchive?.Dispose();
                _azureZipArchive?.Dispose();
                _s3ZipArchiveStream?.Dispose();
                _azureZipArchiveStream?.Dispose();
                _extractDownloader?.Dispose();
            }

            _disposed = true;
        }
    }
}
