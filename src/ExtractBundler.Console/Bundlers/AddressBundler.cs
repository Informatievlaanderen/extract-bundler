namespace ExtractBundler.Console.Bundlers;

using CloudStorageClients;
using HttpClients;

public class AddressBundler : BaseBundler
{
    public AddressBundler(ExtractDownloader extractDownloader, MetaDataCenterHttpClient metadataClient,
        S3Client s3Client, AzureBlobClient azureBlobClient) : base(extractDownloader, metadataClient, s3Client, azureBlobClient)
    {
        _identifier = Identifier.Address;
        _requiredZipArchives.Add("AddressRegistry_Extract");
    }
}
