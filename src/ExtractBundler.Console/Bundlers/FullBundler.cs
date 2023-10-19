namespace ExtractBundler.Console.Bundlers;

using CloudStorageClients;
using HttpClients;
using Infrastructure.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class FullBundler : BaseBundler<FullBundler>
{
    public FullBundler(
        ExtractDownloader extractDownloader,
        MetaDataCenterHttpClient metadataClient,
        S3Client s3Client,
        AzureBlobClient azureBlobClient,
        ILoggerFactory loggerFactory,
        IOptions<AzureBlobOptions> azureOptions)
        : base(extractDownloader, metadataClient, s3Client, azureBlobClient, loggerFactory, azureOptions)
    {
        Identifier = Identifier.Full;
        RequiredZipArchives.AddRange(new[]
        {
            "AddressRegistry_Extract",
            "AddressRegistry_Crab",
            "BuildingRegistry_Extract",
            "BuildingRegistry_Links",
            "MunicipalityRegistry_Extract",
            "ParcelRegistry_Extract",
            "ParcelRegistry_Links",
            "PostalRegistry_Extract",
            "StreetNameRegistry_Extract"
        });
    }
}
