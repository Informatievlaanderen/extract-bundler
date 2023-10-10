namespace ExtractBundler.Console.Bundlers;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using CloudStorageClients;
using HttpClients;

public abstract class BaseBundler : IDisposable
{
    private readonly ExtractDownloader _extractDownloader;
    private readonly MetaDataCenterHttpClient _metadataClient;
    private readonly S3Client _s3Client;
    private readonly AzureBlobClient _azureBlobClient;

    private readonly MemoryStream _s3ZipArchiveStream;
    private readonly ZipArchive _s3ZipArchive;

    private readonly MemoryStream _azureZipArchiveStream;
    private readonly ZipArchive _azureZipArchive;

    private CancellationToken _cancellationToken;
    readonly object _createEntryLock = new object();
    private int _downloadedZipArchives = 0;
    private bool _disposed = false;

    public bool IsComplete { get; set; }
    protected Identifier _identifier;
    protected readonly List<string> _requiredZipArchives = new List<string>();

    protected BaseBundler(
        ExtractDownloader extractDownloader,
        MetaDataCenterHttpClient metadataClient,
        S3Client s3Client,
        AzureBlobClient azureBlobClient)
    {
        _extractDownloader = extractDownloader;
        _metadataClient = metadataClient;
        _s3Client = s3Client;
        _azureBlobClient = azureBlobClient;

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

        //Update MetaDataCenter
        await _metadataClient.UpdateCswPublication(_identifier, DateTime.Now, _cancellationToken);
        Console.WriteLine(_identifier.GetValue(ZipKey.MetadataUpdatedMessage));

        //Download MetaDataCenter files
        var pdfAsBytes = await _metadataClient.GetPdfAsByteArray(_identifier, _cancellationToken);
        var xmlAsString = await _metadataClient.GetXmlAsString(_identifier, _cancellationToken);

        //Append to azure
        await _azureZipArchive.AddToZipArchive(_identifier.GetValue(ZipKey.MetaGrarXml), xmlAsString,
            _cancellationToken);
        await _azureZipArchive.AddToZipArchive(_identifier.GetValue(ZipKey.MetaGrarPdf), pdfAsBytes,
            _cancellationToken);
        Console.WriteLine($"[{_identifier.GetValue(ZipKey.AzureZip)}] ADD {_identifier.GetValue(ZipKey.MetaGrarXml)}");
        Console.WriteLine($"[{_identifier.GetValue(ZipKey.AzureZip)}] ADD {_identifier.GetValue(ZipKey.MetaGrarPdf)}");

        var instructionPdfAsBytes = await File.ReadAllBytesAsync(
            Path.Join(AppDomain.CurrentDomain.BaseDirectory, _identifier.GetValue(ZipKey.InstructionPdf)),
            _cancellationToken);
        await _azureZipArchive.AddToZipArchive(_identifier.GetValue(ZipKey.InstructionPdf), instructionPdfAsBytes,
            _cancellationToken);
        Console.WriteLine(
            $"[{_identifier.GetValue(ZipKey.AzureZip)}] ADD {_identifier.GetValue(ZipKey.InstructionPdf)}");

        _s3ZipArchive.Dispose();
        _azureZipArchive.Dispose();

        var azureZipAsBytes = _azureZipArchiveStream.ToArray();
        var s3ZipAsBytes = _s3ZipArchiveStream.ToArray();

        await _s3ZipArchiveStream.DisposeAsync();
        await _azureZipArchiveStream.DisposeAsync();

        await _azureBlobClient.UploadBlobInChunksAsync(azureZipAsBytes, _identifier, _cancellationToken);
        Console.WriteLine("Upload to Azure Blob completed.");

        await _s3Client.UploadBlobInChunksAsync(s3ZipAsBytes, _identifier, _cancellationToken);
        Console.WriteLine("Upload to S3 Blob completed.");

        Console.WriteLine(_identifier.GetValue(ZipKey.ExtractDoneMessage));

        _disposed = true;
        IsComplete = true;
    }

    private async void ExtractDownloaderOnOnZipArchiveDownloaded(object? sender, EventArgs e)
    {
        if (sender == null)
        {
            throw new InvalidOperationException("ZipArchive is null");
        }

        var (fileName, zipArchive) = (ValueTuple<string, ZipArchive>)sender;

        //No Operation
        if (!_requiredZipArchives.Contains(fileName))
        {
            return;
        }

        foreach (var entry in zipArchive.Entries)
        {
            //Clone the entries to the destination archive
            string entryFileName = _identifier.RewriteZipEntryFullNameForAzure(entry.FullName);
            lock (_createEntryLock)
            {
                Console.WriteLine($"[{_identifier.GetValue(ZipKey.S3Zip)}] ADD {entry.FullName}");
                Console.WriteLine($"[{_identifier.GetValue(ZipKey.AzureZip)}] ADD {entryFileName} ");
                Task.WaitAll(new List<Task>()
                {
                    entry.CopyToAsync(_s3ZipArchive, entry.FullName, _cancellationToken),
                    entry.CopyToAsync(_azureZipArchive, entryFileName, _cancellationToken)
                }.ToArray());
            }
        }

        _downloadedZipArchives++;

        if (_downloadedZipArchives >= _requiredZipArchives.Count)
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
