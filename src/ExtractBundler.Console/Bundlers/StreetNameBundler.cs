namespace ExtractBundler.Console.Bundlers;

using Amazon.Runtime.Internal.Util;
using CloudStorageClients;
using HttpClients;
using Infrastructure.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class StreetNameBundler : BaseBundler<StreetNameBundler>
{
    public StreetNameBundler(
        ExtractDownloader extractDownloader,
        MetaDataCenterHttpClient metadataClient,
        S3Client s3Client,
        AzureBlobClient azureBlobClient,
        ILoggerFactory loggerFactory,
        IOptions<AzureBlobOptions> azureOptions)
        : base(extractDownloader, metadataClient, s3Client, azureBlobClient, loggerFactory, azureOptions)
    {
        Identifier = Identifier.StreetName;
        RequiredZipArchives.AddRange(new[]
        {
            "MunicipalityRegistry_Extract",
            "PostalRegistry_Extract",
            "StreetNameRegistry_Extract"
        });
    }
}
