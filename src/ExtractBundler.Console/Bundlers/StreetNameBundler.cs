namespace ExtractBundler.Console.Bundlers;

using Amazon.Runtime.Internal.Util;
using CloudStorageClients;
using HttpClients;
using Microsoft.Extensions.Logging;

public class StreetNameBundler : BaseBundler<StreetNameBundler>
{
    public StreetNameBundler(
        ExtractDownloader extractDownloader,
        MetaDataCenterHttpClient metadataClient,
        S3Client s3Client,
        AzureBlobClient azureBlobClient,
        ILoggerFactory loggerFactory)
        : base(extractDownloader, metadataClient, s3Client, azureBlobClient, loggerFactory)
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
