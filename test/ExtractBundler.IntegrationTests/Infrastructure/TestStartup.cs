namespace ExtractBundler.IntegrationTests.Infrastructure;

using System.Collections.Generic;
using Console.Bundlers;
using Console.CloudStorageClients;
using Console.HttpClients;
using Console.Infrastructure;
using Console.Infrastructure.Configurations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class TestStartup
{
    private readonly IConfiguration _configuration;

    public TestStartup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        var s3Options = _configuration.GetSection(nameof(S3Options)).Get<S3Options>();
        var azureOptions = _configuration.GetSection(nameof(AzureBlobOptions)).Get<AzureBlobOptions>();

        services.AddScoped<FullBundler>()
            .AddScoped<StreetNameBundler>()
            .AddScoped<AddressBundler>()
            .AddScoped<AddressLinksBundler>()
            .AddTransient<MetaDataCenterHttpClient>()
            .AddSingleton<ExtractDownloader>()
            .AddSingleton<S3Client>()
            .AddSingleton<AzureBlobClient>()
            .AddAmazonS3(s3Options)
            .AddAzureBlob(azureOptions)
            .Configure<S3Options>(_configuration.GetSection(nameof(S3Options)))
            .Configure<Dictionary<string, ApiEndPointOptions>>(
                _configuration.GetSection(nameof(ApiEndPointOptions)))
            .Configure<AzureBlobOptions>(_configuration.GetSection(nameof(AzureBlobOptions)))
            .Configure<MetadataCenterOptions>(
                _configuration.GetSection(nameof(MetadataCenterOptions)));
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // This method needs to exist for unit tests but can be empty
    }
}
