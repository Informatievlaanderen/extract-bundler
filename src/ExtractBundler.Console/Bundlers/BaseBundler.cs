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

public abstract class BaseBundler<T> : IDisposable
{
    private readonly ExtractDownloader _extractDownloader;
    private readonly MetaDataCenterHttpClient _metadataClient;
    private readonly S3Client _s3Client;
    private readonly AzureBlobClient _azureBlobClient;
    private readonly AzureBlobOptions _azureOptions;

    private readonly MemoryStream _s3ZipArchiveStream;
    private readonly ZipArchive _s3ZipArchive;

    private readonly MemoryStream _azureZipArchiveStream;
    private readonly ZipArchive _azureZipArchive;

    private CancellationToken _cancellationToken;
    private readonly object _createEntryLock = new object();
    private int _downloadedZipArchives = 0;
    private bool _disposed = false;
    private readonly ILogger<T> _logger;

    protected Identifier Identifier { get; set; }
    protected List<string> RequiredZipArchives { get; } = new();
    public bool IsComplete { get; set; }

    protected BaseBundler(
        ExtractDownloader extractDownloader,
        MetaDataCenterHttpClient metadataClient,
        S3Client s3Client,
        AzureBlobClient azureBlobClient,
        ILoggerFactory loggerFactory,
        IOptions<AzureBlobOptions> azureOptions)
    {
        _logger = loggerFactory.CreateLogger<T>();
        _extractDownloader = extractDownloader;
        _metadataClient = metadataClient;
        _s3Client = s3Client;
        _azureBlobClient = azureBlobClient;
        _azureOptions = azureOptions.Value;

        _s3ZipArchiveStream = new MemoryStream();
        _s3ZipArchive = new ZipArchive(_s3ZipArchiveStream, ZipArchiveMode.Create);

        _azureZipArchiveStream = new MemoryStream();
        _azureZipArchive = new ZipArchive(_azureZipArchiveStream, ZipArchiveMode.Create);

        //EventHandlers
        _extractDownloader.OnZipArchiveDownloaded += ExtractDownloaderOnOnZipArchiveDownloaded;
        _extractDownloader.OnZipArchiveDownloadFailed += ExtractDownloaderOnOnZipArchiveDownloadFailed;
    }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;

        await _extractDownloader.DownloadAll(cancellationToken);

        while (!(IsComplete || _cancellationToken.IsCancellationRequested))
        {
            await Task.Delay(500, _cancellationToken);
        }

        await Task.CompletedTask;
    }

    private async Task GenerateZips()
    {
        //Stop eventHandlers
        _extractDownloader.OnZipArchiveDownloaded -= ExtractDownloaderOnOnZipArchiveDownloaded;
        _extractDownloader.OnZipArchiveDownloadFailed -= ExtractDownloaderOnOnZipArchiveDownloadFailed;

        await UploadToS3();

        if (_azureOptions.Enabled)
        {
            await UploadToAzure();
        }

        _disposed = true;
        IsComplete = true;
    }

    private async Task UploadToS3()
    {
        _s3ZipArchive.Dispose();

        var s3ZipAsBytes = _s3ZipArchiveStream.ToArray();

        await _s3ZipArchiveStream.DisposeAsync();

        await _s3Client.UploadBlobInChunksAsync(s3ZipAsBytes, Identifier, _cancellationToken);
        _logger.LogInformation("Upload to S3 Blob completed.");

        _logger.LogInformation(Identifier.GetValue(ZipKey.ExtractDoneMessage));
    }

    private async Task UploadToAzure()
    {
        //Update MetaDataCenter
        var results = await _metadataClient.UpdateCswPublication(
            Identifier,
            DateTime.Now,
            _cancellationToken);
        if (results == null)
        {
            _logger.LogCritical(Identifier.GetValue(ZipKey.MetadataUpdatedMessageFailed));
            return;
        }

        _logger.LogInformation(Identifier.GetValue(ZipKey.MetadataUpdatedMessage));

        //Download MetaDataCenter files
        var pdfAsBytes = await _metadataClient.GetPdfAsByteArray(Identifier, _cancellationToken);
        var xmlAsString = await _metadataClient.GetXmlAsString(Identifier, _cancellationToken);

        _logger.LogInformation($"[{Identifier.GetValue(ZipKey.AzureZip)}] ADD {Identifier.GetValue(ZipKey.MetaGrarXml)}");
        _logger.LogInformation($"[{Identifier.GetValue(ZipKey.AzureZip)}] ADD {Identifier.GetValue(ZipKey.MetaGrarPdf)}");

        //Append to azure
        await _azureZipArchive.AddToZipArchive(
            Identifier.GetValue(ZipKey.MetaGrarXml),
            xmlAsString,
            _cancellationToken);

        await _azureZipArchive.AddToZipArchive(
            Identifier.GetValue(ZipKey.MetaGrarPdf),
            pdfAsBytes,
            _cancellationToken);

        var instructionPdfAsBytes = await File.ReadAllBytesAsync(
            Path.Join(AppDomain.CurrentDomain.BaseDirectory, Identifier.GetValue(ZipKey.InstructionPdf)),
            _cancellationToken);

        _logger.LogInformation(
            $"[{Identifier.GetValue(ZipKey.AzureZip)}] ADD {Identifier.GetValue(ZipKey.InstructionPdf)}");

        await _azureZipArchive.AddToZipArchive(
            Identifier.GetValue(ZipKey.InstructionPdf),
            instructionPdfAsBytes,
            _cancellationToken);

        _azureZipArchive.Dispose();

        var azureZipAsBytes = _azureZipArchiveStream.ToArray();

        await _azureZipArchiveStream.DisposeAsync();

        await _azureBlobClient.UploadBlobInChunksAsync(azureZipAsBytes, Identifier, _cancellationToken);
        _logger.LogInformation("Upload to Azure Blob completed.");
    }

    private async void ExtractDownloaderOnOnZipArchiveDownloaded(object? sender, EventArgs e)
    {
        if (sender == null)
        {
            throw new InvalidOperationException("ZipArchive is null");
        }

        var (fileName, zipArchive) = (ValueTuple<string, ZipArchive>)sender;

        //No Operation
        if (!RequiredZipArchives.Contains(fileName))
        {
            return;
        }

        foreach (var entry in zipArchive.Entries)
        {
            //Clone the entries to the destination archive
            string entryFileName = Identifier.RewriteZipEntryFullNameForAzure(entry.FullName);
            lock (_createEntryLock)
            {
                _logger.LogInformation($"[{Identifier.GetValue(ZipKey.S3Zip)}] ADD {entry.FullName}");
                _logger.LogInformation($"[{Identifier.GetValue(ZipKey.AzureZip)}] ADD {entryFileName} ");
                Task.WaitAll(new List<Task>()
                {
                    entry.CopyToAsync(_s3ZipArchive, entry.FullName, _cancellationToken),
                    entry.CopyToAsync(_azureZipArchive, entryFileName, _cancellationToken)
                }.ToArray());
            }
        }

        _downloadedZipArchives++;

        if (_downloadedZipArchives >= RequiredZipArchives.Count)
        {
            await GenerateZips();
        }
    }

    private void ExtractDownloaderOnOnZipArchiveDownloadFailed(object? sender, EventArgs e)
    {
        throw new OperationCanceledException();
    }

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
