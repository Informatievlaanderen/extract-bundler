namespace ExtractBundler.Console.Bundlers;

using CloudStorageClients;
using HttpClients;
using Infrastructure.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class FullBundler : BaseBundler
{
    public FullBundler(
        IOptions<BundlerOptions> options,
        ExtractDownloader extractDownloader,
        MetaDataCenterHttpClient metadataClient,
        S3Client s3Client,
        AzureBlobClient azureBlobClient,
        ILoggerFactory loggerFactory,
        IOptions<AzureBlobOptions> azureOptions)
        : base(
            metadataClient,
            s3Client,
            azureBlobClient,
            loggerFactory,
            azureOptions,
            extractDownloader,
            options.Value.Full)
    {
    }

    protected override Identifier GetIdentifier() => Identifier.Full;
}
