namespace ExtractBundler.Console.Bundlers;

using CloudStorageClients;
using HttpClients;
using Infrastructure.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class AddressLinksBundler : BaseBundler
{
    public AddressLinksBundler(
        IOptions<BundlerOptions> options,
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
            options.Value.AddressLinks)
    {
    }

    protected override Identifier GetIdentifier() => Identifier.AddressLinks;
}
