namespace ExtractBundler.Console.Bundlers;

using CloudStorageClients;
using HttpClients;
using Infrastructure.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class AddressLinksBundler : BaseBundler<AddressLinksBundler>
{
    public AddressLinksBundler(
        ExtractDownloader extractDownloader,
        MetaDataCenterHttpClient metadataClient,
        S3Client s3Client,
        AzureBlobClient azureBlobClient,
        ILoggerFactory loggerFactory,
        IOptions<AzureBlobOptions> azureOptions)
        : base(extractDownloader, metadataClient, s3Client, azureBlobClient, loggerFactory, azureOptions)
    {
        Identifier = Identifier.AddressLinks;
        RequiredZipArchives.AddRange(new[]
        {
            "AddressRegistry_Extract",
            "BuildingRegistry_Links",
            "ParcelRegistry_Links"
        });
    }
}
