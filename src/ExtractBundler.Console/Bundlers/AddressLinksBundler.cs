namespace ExtractBundler.Console.Bundlers;

using CloudStorageClients;
using HttpClients;

public class AddressLinksBundler : BaseBundler
{
    public AddressLinksBundler(ExtractDownloader extractDownloader, MetaDataCenterHttpClient metadataClient,
        S3Client s3Client, AzureBlobClient azureBlobClient) : base(extractDownloader, metadataClient, s3Client, azureBlobClient)
    {
        _identifier = Identifier.AddressLinks;
        _requiredZipArchives.AddRange(new []
        {
            "AddressRegistry_Extract",
            "BuildingRegistry_Links",
            "ParcelRegistry_Links"
        });
    }
}
