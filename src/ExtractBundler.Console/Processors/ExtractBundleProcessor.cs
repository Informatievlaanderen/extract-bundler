namespace ExtractBundler.Console.Processors;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bundlers;
using Infrastructure.Configurations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class ExtractBundleProcessor : BackgroundService
{
    private readonly ILogger<ExtractBundleProcessor> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly FullBundler _fullBundler;
    private readonly StreetNameBundler _streetNameBundler;
    private readonly AddressBundler _addressBundler;
    private readonly AddressLinksBundler _addressLinksBundler;
    private readonly BundlerEnableOptions _options;

    public ExtractBundleProcessor(
        IOptions<BundlerEnableOptions> options,
        FullBundler fullBundler,
        StreetNameBundler streetNameBundler,
        AddressBundler addressBundler,
        AddressLinksBundler addressLinksBundler,
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime hostApplicationLifetime)
    {
        _options = options.Value;
        _fullBundler = fullBundler;
        _streetNameBundler = streetNameBundler;
        _addressBundler = addressBundler;
        _addressLinksBundler = addressLinksBundler;
        _hostApplicationLifetime = hostApplicationLifetime;
        _logger = loggerFactory.CreateLogger<ExtractBundleProcessor>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bundlerTasks = new List<Task>();
        if (_options.StreetName)
        {
            bundlerTasks.Add(_streetNameBundler.Start(stoppingToken));
        }
        if (_options.Address)
        {
            bundlerTasks.Add(_addressBundler.Start(stoppingToken));
        }
        if (_options.AddressLinks)
        {
            bundlerTasks.Add(_addressLinksBundler.Start(stoppingToken));
        }
        if (_options.Full)
        {
            bundlerTasks.Add(_fullBundler.Start(stoppingToken));
        }
        await Task.WhenAll(bundlerTasks);
        _logger.LogWarning("Zips complete. See you later alligator!");
        _hostApplicationLifetime.StopApplication();
    }
}
