namespace ExtractBundler.Console.Bundlers;

using CloudStorageClients;
using HttpClients;

public class FullBundler : BaseBundler
{
    public FullBundler(ExtractDownloader extractDownloader, MetaDataCenterHttpClient metadataClient,
        S3Client s3Client, AzureBlobClient azureBlobClient) : base(extractDownloader, metadataClient, s3Client, azureBlobClient)
    {
        _identifier = Identifier.Full;
        _requiredZipArchives.AddRange(new[]
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
