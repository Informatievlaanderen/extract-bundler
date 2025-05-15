namespace ExtractBundler.Console.CloudStorageClients
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Amazon.S3.Transfer;
    using Infrastructure.Configurations;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public class S3Client
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly S3Options _options;
        private readonly ILogger<S3Client> _logger;

        public S3Client(IAmazonS3 amazonS3, IOptions<S3Options> options, ILoggerFactory loggerFactory)
        {
            _amazonS3 = amazonS3;
            _options = options.Value;
            _logger = loggerFactory.CreateLogger<S3Client>();
        }

        public async Task UploadBlobInChunksAsync(MemoryStream stream, Identifier identifier,
            CancellationToken token = default)
        {
            using (var transferUtility = new TransferUtility(_amazonS3))
            {
                try
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    var request = new TransferUtilityUploadRequest()
                    {
                        BucketName = _options.BucketName,
                        Key = identifier.GetValue(ZipKey.S3Zip),
                        InputStream = stream,
                        ContentType = "application/octet-stream",
                        AutoCloseStream = false,
                    };
                    await transferUtility.UploadAsync(request, token);
                }
                catch (AmazonS3Exception e)
                {
                    _logger.LogError("Error encountered on server. Message:'{e.Message}' when writing an object", e);
                }
                catch (Exception e)
                {
                    _logger.LogError("Unknown encountered on server. Message:'{e.Message}' when writing an object", e);
                }
            }
        }

        public async Task<bool> DoesBlobExists(Identifier identifier, CancellationToken cancellationToken)
        {
            var obj = await _amazonS3.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = _options.BucketName,
                    Prefix = identifier.GetValue(ZipKey.S3Zip),
                    MaxKeys = 1
                },
                cancellationToken);

            return obj.KeyCount > 0;
        }

        public async Task<byte[]?> GetZipArchiveInBytesFromS3Async(Identifier identifier,
            CancellationToken cancellationToken = default)
        {
            var obj = await _amazonS3.GetObjectAsync(_options.BucketName, identifier.GetValue(ZipKey.S3Zip),
                cancellationToken);
            if (obj == null)
            {
                return null;
            }

            await using var destStream = new MemoryStream();
            await using var stream = obj.ResponseStream;
            await stream.CopyToAsync(destStream, cancellationToken);
            return destStream.ToArray();
        }
    }
}
