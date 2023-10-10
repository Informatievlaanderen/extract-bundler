namespace ExtractBundler.Console.Bundlers;

using CloudStorageClients;
using HttpClients;
using Microsoft.Extensions.Logging;

public class AddressLinksBundler : BaseBundler<AddressLinksBundler>
{
    public AddressLinksBundler(
        ExtractDownloader extractDownloader,
        MetaDataCenterHttpClient metadataClient,
        S3Client s3Client,
        AzureBlobClient azureBlobClient,
        ILoggerFactory loggerFactory)
        : base(extractDownloader, metadataClient, s3Client, azureBlobClient, loggerFactory)
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
