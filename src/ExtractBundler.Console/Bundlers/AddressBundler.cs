namespace ExtractBundler.Console.Bundlers;

using CloudStorageClients;
using HttpClients;
using Infrastructure.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class AddressBundler : BaseBundler<AddressBundler>
{
    public AddressBundler(
        ExtractDownloader extractDownloader,
        MetaDataCenterHttpClient metadataClient,
        S3Client s3Client,
        AzureBlobClient azureBlobClient,
        ILoggerFactory loggerFactory,
        IOptions<AzureBlobOptions> azureOptions)
        : base(extractDownloader, metadataClient, s3Client, azureBlobClient, loggerFactory, azureOptions)
    {
        Identifier = Identifier.Address;
        RequiredZipArchives.Add("AddressRegistry_Extract");
    }
}
