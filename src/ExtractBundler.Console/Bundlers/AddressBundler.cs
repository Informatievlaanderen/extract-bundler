namespace ExtractBundler.Console.Bundlers;

using CloudStorageClients;
using HttpClients;
using Microsoft.Extensions.Logging;

public class AddressBundler : BaseBundler<AddressBundler>
{
    public AddressBundler(
        ExtractDownloader extractDownloader,
        MetaDataCenterHttpClient metadataClient,
        S3Client s3Client,
        AzureBlobClient azureBlobClient,
        ILoggerFactory loggerFactory)
        : base(extractDownloader, metadataClient, s3Client, azureBlobClient, loggerFactory)
    {
        Identifier = Identifier.Address;
        RequiredZipArchives.Add("AddressRegistry_Extract");
    }
}
