namespace ExtractBundler.Console.HttpClients;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime.Internal;
using Microsoft.Extensions.Logging;

public sealed class ExtractDownloader : IDisposable
{
    private readonly ILogger<ExtractDownloader>? _logger;
    private readonly HttpClient _httpClient;
    private bool _disposed = false;

    public ExtractDownloader(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ExtractDownloader>();
        _httpClient = new HttpClient();
    }

    public async Task DownloadAllAsync(
        IEnumerable<string> urls,
        Func<Stream, CancellationToken, Task> contentStreamHandlerAsync,
        CancellationToken cancellationToken = default)
    {
        foreach (var url in urls)
        {
            var response = await _httpClient
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            try
            {
                response.EnsureSuccessStatusCode();

                using var httpContent = response.Content;
                await using var downloadStream = await httpContent.ReadAsStreamAsync(cancellationToken);
                await contentStreamHandlerAsync(downloadStream, cancellationToken);

                _logger?.LogInformation("Successfully downloaded from {url}", url);
            }
            catch (HttpRequestException ex)
            {
                _logger?.LogCritical(ex, $"zipfile : {url}");
            }
        }

        _logger?.LogWarning("All zips have been downloaded");
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
            }

            _disposed = true;
        }
    }
}
