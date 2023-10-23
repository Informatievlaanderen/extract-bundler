namespace ExtractBundler.Console.HttpClients;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class ExtractDownloader
{
    private readonly ILogger<ExtractDownloader>? _logger;
    private readonly Dictionary<string, Dictionary<string, ApiEndPointOptionItem>> _apiEndPointOptions;
    private readonly Dictionary<string, ZipArchive> _zipArchives = new();
    private bool _isRunning;

    public ExtractDownloader(
        IOptions<Dictionary<string, Dictionary<string, ApiEndPointOptionItem>>> options,
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

        var flattendEndpoints = _apiEndPointOptions.SelectMany(i =>
            i.Value.ToDictionary(k => $"{i.Key}_{k.Key}", v => v.Value));

        var groupedEndpoints = flattendEndpoints
            .Where(i => i.Value.Enabled)
            .OrderBy(i => i.Value.PriorityGroup)
            .GroupBy(i => i.Value.PriorityGroup)
            .ToList();

        foreach (var priorityGroup in groupedEndpoints)
        {
            _logger?.LogWarning($"Start download endpoint Group {priorityGroup.Key}");

            var groupTask = priorityGroup
                .Select(endpoint => Download(endpoint.Value.Url, endpoint.Key, cancellationToken))
                .ToArray();
            await Task.WhenAll(groupTask);

            _logger?.LogWarning($"Endpoint Group {priorityGroup.Key} has been downloaded");
        }

        _logger?.LogWarning("All zips have been downloaded");
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
                _logger?.LogWarning($"Download Complete {fileName}.zip ({totalBytesRead.FormatBytes()})");
                continue;
            }

            await memoryStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesRead += bytesRead;
            ++readCount;

            if (readCount % 1000 == 0) //Less aggressive logging
            {
                _logger?.LogInformation($"Download {fileName}.zip ({totalBytesRead.FormatBytes()})");
            }
        } while (isMoreToRead);

        var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Read);
        return zipArchive;
    }
}
