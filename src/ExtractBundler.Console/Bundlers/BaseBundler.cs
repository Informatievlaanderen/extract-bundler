namespace ExtractBundler.Console.Bundlers;

using System;
using System.Collections.Generic;
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
        _s3ZipArchive = new ZipArchive(_s3ZipArchiveStream, ZipArchiveMode.Create);

        _azureZipArchiveStream = new MemoryStream();
        _azureZipArchive = new ZipArchive(_azureZipArchiveStream, ZipArchiveMode.Create);
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        if (!_bundlerOption.Enabled)
        {
            return;
        }

        await foreach (var zipArchiveInBytes in _extractDownloader.DownloadAll(_bundlerOption.UrlsToList(),
                           cancellationToken))
        {
            await GenerateZipArchivesAndUpload(zipArchiveInBytes, cancellationToken).ConfigureAwait(false);
        }

        await UploadToS3(cancellationToken);

        if (_azureOptions.Enabled)
        {
            await UploadToAzure(cancellationToken);
        }

        _disposed = true;
    }

    private Task GenerateZipArchivesAndUpload(byte[] zipArchiveInBytes,
        CancellationToken cancellationToken = default)
    {
        using var zipArchiveStream = new MemoryStream(zipArchiveInBytes);
        using var zipArchive = new ZipArchive(zipArchiveStream, ZipArchiveMode.Read);
        foreach (var entry in zipArchive.Entries)
        {
            //Clone the entries to the destination archive
            string entryFileName = GetIdentifier().RewriteZipEntryFullNameForAzure(entry.FullName);
            _logger.LogWarning($"[{GetIdentifier().GetValue(ZipKey.S3Zip)}] ADD {entry.FullName}");
            _logger.LogWarning($"[{GetIdentifier().GetValue(ZipKey.AzureZip)}] ADD {entryFileName} ");
            Task.WaitAll(new List<Task>()
            {
                entry.CopyToAsync(_s3ZipArchive, entry.FullName, cancellationToken),
                entry.CopyToAsync(_azureZipArchive, entryFileName, cancellationToken)
            }.ToArray());
        }

        return Task.CompletedTask;
    }

    private async Task UploadToS3(CancellationToken cancellationToken)
    {
        _s3ZipArchive.Dispose();

        var s3ZipAsBytes = _s3ZipArchiveStream.ToArray();

        await _s3ZipArchiveStream.DisposeAsync();

        await _s3Client.UploadBlobInChunksAsync(s3ZipAsBytes, GetIdentifier(), cancellationToken);
        _logger.LogWarning("Upload to S3 Blob completed.");

        _logger.LogWarning(GetIdentifier().GetValue(ZipKey.ExtractDoneMessage));
    }

    private async Task UploadToAzure(CancellationToken cancellationToken)
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

        _azureZipArchive.Dispose();

        var azureZipAsBytes = _azureZipArchiveStream.ToArray();

        await _azureZipArchiveStream.DisposeAsync();

        await _azureBlobClient.UploadBlobInChunksAsync(azureZipAsBytes, GetIdentifier(), cancellationToken);
        _logger.LogWarning("Upload to Azure Blob completed.");
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
            }

            _disposed = true;
        }
    }
}
