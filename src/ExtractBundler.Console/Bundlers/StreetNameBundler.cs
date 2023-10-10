namespace ExtractBundler.Console.Bundlers;

using CloudStorageClients;
using HttpClients;

public class StreetNameBundler : BaseBundler
{
    public StreetNameBundler(ExtractDownloader extractDownloader, MetaDataCenterHttpClient metadataClient,
        S3Client s3Client, AzureBlobClient azureBlobClient) : base(extractDownloader, metadataClient, s3Client, azureBlobClient)
    {
        _identifier = Identifier.StreetName;
        _requiredZipArchives.AddRange(new []
        {
            "MunicipalityRegistry_Extract",
            "PostalRegistry_Extract",
            "StreetNameRegistry_Extract"
        });
    }
}
