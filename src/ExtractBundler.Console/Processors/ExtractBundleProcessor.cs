namespace ExtractBundler.Console.Processors;

using System.Threading;
using System.Threading.Tasks;
using Bundlers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class ExtractBundleProcessor : BackgroundService
{
    private readonly ILogger<ExtractBundleProcessor> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly StreetNameBundler streetNameBundler;
    private readonly AddressBundler addressBundler;
    private readonly AddressLinksBundler addressLinksBundler;
    private readonly FullBundler _fullBundler;

    public ExtractBundleProcessor(
        StreetNameBundler streetNameBundler,
        AddressBundler addressBundler,
        AddressLinksBundler addressLinksBundler,
        FullBundler fullBundler,
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime hostApplicationLifetime)
    {
        this.streetNameBundler = streetNameBundler;
        this.addressBundler = addressBundler;
        this.addressLinksBundler = addressLinksBundler;
        _fullBundler = fullBundler;
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = loggerFactory.CreateLogger<ExtractBundleProcessor>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await streetNameBundler.Start(stoppingToken);
        streetNameBundler.Dispose();

        await addressBundler.Start(stoppingToken);
        addressBundler.Dispose();

        await addressLinksBundler.Start(stoppingToken);
        addressLinksBundler.Dispose();

        await _fullBundler.Start(stoppingToken);
        _fullBundler.Dispose();

        _logger.LogWarning("Zips complete. See you later alligator!");
        _hostApplicationLifetime.StopApplication();
    }
}
