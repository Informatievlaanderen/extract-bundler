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
    private readonly FullBundler _fullBundler;
    private readonly StreetNameBundler _streetNameBundler;
    private readonly AddressBundler _addressBundler;
    private readonly AddressLinksBundler _addressLinksBundler;

    public ExtractBundleProcessor(
        FullBundler fullBundler,
        StreetNameBundler streetNameBundler,
        AddressBundler addressBundler,
        AddressLinksBundler addressLinksBundler,
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime hostApplicationLifetime)
    {
        _fullBundler = fullBundler;
        _streetNameBundler = streetNameBundler;
        _addressBundler = addressBundler;
        _addressLinksBundler = addressLinksBundler;
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = loggerFactory.CreateLogger<ExtractBundleProcessor>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.WhenAll(
            _fullBundler.Start(stoppingToken),
            _streetNameBundler.Start(stoppingToken),
            _addressBundler.Start(stoppingToken),
            _addressLinksBundler.Start(stoppingToken));

        _logger.LogInformation("Zips complete. See you later alligator!");
        _hostApplicationLifetime.StopApplication();
    }
}
