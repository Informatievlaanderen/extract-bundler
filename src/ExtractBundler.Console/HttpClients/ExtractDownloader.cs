namespace ExtractBundler.Console.HttpClients;

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public sealed class ExtractDownloader
{
    private readonly ILogger<ExtractDownloader>? _logger;

    public ExtractDownloader(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ExtractDownloader>();
    }

    public async IAsyncEnumerable<byte[]> DownloadAll(IEnumerable<string> urls,CancellationToken cancellationToken = default)
    {
        foreach (var url in urls)
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var zipArchiveStream = await GetZipArchiveWithDownloadProgressAsync(response!, cancellationToken);
                yield return zipArchiveStream;
            }
            _logger?.LogCritical($"zipfile : {url}");
        }
        _logger?.LogWarning("All zips have been downloaded");
    }

    private async Task<byte[]> GetZipArchiveWithDownloadProgressAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
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
                _logger?.LogWarning($"Download Complete zipfile ({totalBytesRead.FormatBytes()})");
                continue;
            }

            await memoryStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesRead += bytesRead;
            ++readCount;

            if (readCount % 1000 == 0) //Less aggressive logging
            {
                _logger?.LogInformation($"Download zipfile ({totalBytesRead.FormatBytes()})");
            }
        } while (isMoreToRead);

        return memoryStream.ToArray();
    }
}
