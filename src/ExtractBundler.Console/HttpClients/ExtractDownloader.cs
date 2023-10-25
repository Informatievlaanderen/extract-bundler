namespace ExtractBundler.Console.HttpClients;

using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
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

    public async IAsyncEnumerable<Stream> DownloadAll(IEnumerable<string> urls,[EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var url in urls)
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var c = response.Content;
                var r = await c.ReadAsStreamAsync(cancellationToken);
                yield return r;
            }
            _logger?.LogCritical($"zipfile : {url}");
        }
        _logger?.LogWarning("All zips have been downloaded");
    }
}
