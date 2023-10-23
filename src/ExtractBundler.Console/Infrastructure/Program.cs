namespace ExtractBundler.Console.Infrastructure;

using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Azure.Identity;
using Azure.Storage.Blobs;
using Be.Vlaanderen.Basisregisters.Aws.DistributedMutex;
using Be.Vlaanderen.Basisregisters.DataDog.Tracing.Autofac;
using Bundlers;
using CloudStorageClients;
using Configurations;
using Destructurama;
using HttpClients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Processors;
using Serilog;
using Serilog.Debugging;

public sealed class Program
{
    private Program()
    {
    }

    public static async Task Main(string[] args)
    {
        AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) =>
            Log.Debug(
                eventArgs.Exception,
                "FirstChanceException event raised in {AppDomain}.",
                AppDomain.CurrentDomain.FriendlyName);

        AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
            Log.Fatal((Exception)eventArgs.ExceptionObject, "Encountered a fatal exception, exiting program.");

        Log.Information("Starting Upload Processor.");

        var host = new HostBuilder()
            .ConfigureAppConfiguration((hostingContext, builder) =>
            {
                var env = hostingContext.HostingEnvironment;

                builder
                    .SetBasePath(env.ContentRootPath)
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .AddJsonFile($"appsettings.{env.EnvironmentName.ToLowerInvariant()}.json", optional: true,
                        reloadOnChange: false)
                    .AddJsonFile($"appsettings.{Environment.MachineName.ToLowerInvariant()}.json", optional: true,
                        reloadOnChange: false)
                    .AddEnvironmentVariables()
                    .AddCommandLine(args);
            })
            .ConfigureLogging((hostContext, builder) =>
            {
                SelfLog.Enable(Console.WriteLine);

                Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(hostContext.Configuration)
                    .Enrich.FromLogContext()
                    .Enrich.WithMachineName()
                    .Enrich.WithThreadId()
                    .Enrich.WithEnvironmentUserName()
                    .Destructure.JsonNetTypes()
                    .CreateLogger();

                builder.ClearProviders();
                builder.AddSerilog(Log.Logger);
            })
            .ConfigureServices((hostContext, services) =>
            {
                var s3Options = hostContext.Configuration.GetSection(nameof(S3Options)).Get<S3Options>();
                var azureOptions = hostContext.Configuration.GetSection(nameof(AzureBlobOptions))
                    .Get<AzureBlobOptions>();
                services
                    .AddScoped<FullBundler>()
                    .AddScoped<StreetNameBundler>()
                    .AddScoped<AddressBundler>()
                    .AddScoped<AddressLinksBundler>()
                    .AddTransient<MetaDataCenterHttpClient>()
                    .AddTransient<ExtractDownloader>()
                    .AddAmazonS3(s3Options)
                    .AddAzureBlob(azureOptions)
                    .AddSingleton<S3Client>()
                    .AddSingleton<AzureBlobClient>()
                    .Configure<S3Options>(hostContext.Configuration.GetSection(nameof(S3Options)))
                    .Configure<AzureBlobOptions>(hostContext.Configuration.GetSection(nameof(AzureBlobOptions)))
                    .Configure<BundlerOptions>(hostContext.Configuration.GetSection(nameof(BundlerOptions)))
                    .Configure<MetadataCenterOptions>(
                        hostContext.Configuration.GetSection(nameof(MetadataCenterOptions)));
                services.AddHostedService<ExtractBundleProcessor>();
            })
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .ConfigureContainer<ContainerBuilder>((hostContext, builder) =>
            {
                var services = new ServiceCollection();
                builder
                    .RegisterModule(new DataDogModule(hostContext.Configuration));

                builder.Populate(services);
            })
            .UseConsoleLifetime()
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();

        try
        {
            await DistributedLock<Program>.RunAsync(
                    async () => { await host.RunAsync().ConfigureAwait(false); },
                    DistributedLockOptions.LoadFromConfiguration(configuration),
                    logger)
                .ConfigureAwait(false);
        }
        catch (AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.InnerExceptions)
            {
                logger.LogCritical(innerException, "Encountered a fatal exception, exiting program.");
            }
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Encountered a fatal exception, exiting program.");
            Log.CloseAndFlush();

            // Allow some time for flushing before shutdown.
            await Task.Delay(500, default);
            throw;
        }
        finally
        {
            logger.LogWarning("Stopping...");
        }
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAmazonS3(this IServiceCollection services, S3Options options)
    {
        bool isDevelopment = !string.IsNullOrWhiteSpace(options.AccessKey) &&
                             !string.IsNullOrWhiteSpace(options.AccessSecret);


        if (!isDevelopment)
        {
            return services.AddSingleton<IAmazonS3>(i => new AmazonS3Client(
                new AmazonS3Config
                {
                    RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region)
                }));
        }

        //Development mode without minio
        if (!options.IsMinio)
        {
            return services.AddSingleton<IAmazonS3>(i => new AmazonS3Client(
                new BasicAWSCredentials(options.AccessKey,
                    options.AccessSecret),
                new AmazonS3Config
                {
                    RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region)
                }));
        }

        //Development mode with minio
        return services.AddSingleton<IAmazonS3>(i => new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKey,
                options.AccessSecret),
            new AmazonS3Config
            {
                ServiceURL = options.BaseUrl,
                DisableHostPrefixInjection = true,
                ForcePathStyle = true,
                LogResponse = true
            }));
    }

    public static IServiceCollection AddAzureBlob(this IServiceCollection services, AzureBlobOptions options)
    {
        if (options.IsAzurite)
        {
            return services.AddSingleton(i => new BlobServiceClient(
                options.ConnectionString,
                new BlobClientOptions(BlobClientOptions.ServiceVersion.V2020_04_08)));
        }

        return services.AddSingleton(i => new BlobServiceClient(
            new Uri(options.BaseUrl),
            new ClientSecretCredential(options.TenantId, options.ClientKey, options.ClientSecret),
            new BlobClientOptions(BlobClientOptions.ServiceVersion.V2020_04_08)));
    }
}
