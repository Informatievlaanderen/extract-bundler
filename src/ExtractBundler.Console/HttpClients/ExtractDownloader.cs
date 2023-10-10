namespace ExtractBundler.Console.HttpClients;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class ExtractDownloader
{
    private readonly ILogger<ExtractDownloader>? _logger;
    private readonly Dictionary<string, ApiEndPointOptions> _apiEndPointOptions;
    private readonly Dictionary<string, ZipArchive> _zipArchives = new();
    private bool _isRunning;

    public ExtractDownloader(
        IOptions<Dictionary<string, ApiEndPointOptions>> options,
        ILoggerFactory loggerFactory)
    {
        _apiEndPointOptions = options.Value;
        _logger = loggerFactory.CreateLogger<ExtractDownloader>();
    }

    public event EventHandler? OnZipArchiveDownloaded;
    public event EventHandler? OnZipArchiveDownloadFailed;

    public async Task DownloadAll(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
            return;
        _isRunning = true;

        var tasks = new List<Task>();

        foreach (var (registry, endpoints) in _apiEndPointOptions)
        {
            if (!string.IsNullOrWhiteSpace(endpoints.Extract))
            {
                tasks.Add(Download(endpoints.Extract, registry + "_Extract", cancellationToken));
            }

            if (!string.IsNullOrWhiteSpace(endpoints.Links))
            {
                tasks.Add(Download(endpoints.Links, registry + "_Links", cancellationToken));
            }

            if (!string.IsNullOrWhiteSpace(endpoints.Crab))
            {
                tasks.Add(Download(endpoints.Crab, registry + "_Crab", cancellationToken));
            }
        }

        await Task.WhenAll(tasks);
        _logger?.LogInformation("All zips have been downloaded");
    }

    private async Task Download(string url, string fileName, CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var archive = await GetZipArchiveWithDownloadProgressAsync(response!, fileName, cancellationToken);
            _zipArchives.Add(fileName, archive);
            OnZipArchiveDownloaded?.Invoke((fileName, archive), EventArgs.Empty);
            return;
        }

        OnZipArchiveDownloadFailed?.Invoke($"{fileName} : {url}", EventArgs.Empty);
    }

    private async Task<ZipArchive> GetZipArchiveWithDownloadProgressAsync(
        HttpResponseMessage response,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var memoryStream = new MemoryStream();
        long totalBytesRead = 0L, readCount = 0L;
        var buffer = new byte[8192];
        var isMoreToRead = true;
        var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        do
        {
            var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            if (bytesRead == 0)
            {
                isMoreToRead = false;
                _logger?.LogInformation($"Download Complete {fileName}.zip ({totalBytesRead.FormatBytes()})");
                continue;
            }

            await memoryStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesRead += bytesRead;
            ++readCount;

            if (readCount % 100 == 0)
            {
                _logger?.LogInformation($"Download {fileName}.zip ({totalBytesRead.FormatBytes()})");
            }
        } while (isMoreToRead);

        var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
        return zipArchive;
    }
}
