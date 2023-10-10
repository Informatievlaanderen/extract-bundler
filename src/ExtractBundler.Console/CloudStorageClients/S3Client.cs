namespace ExtractBundler.Console.CloudStorageClients
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.S3.Transfer;
    using Infrastructure.Configurations;
    using Microsoft.Extensions.Options;

    public class S3Client
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly S3Options _options;

        public S3Client(IAmazonS3 amazonS3, IOptions<S3Options> options)
        {
            _amazonS3 = amazonS3;
            _options = options.Value;
        }

        public async Task UploadBlobInChunksAsync(byte[] content, Identifier identifier, CancellationToken token = default)
        {
            using var transferUtility = new TransferUtility(_amazonS3);
            try
            {
                using var stream = new MemoryStream(content);
                stream.Seek(0, SeekOrigin.Begin);
                var request = new TransferUtilityUploadRequest()
                {
                    BucketName = _options.BucketName,
                    Key = identifier.GetValue(ZipKey.S3Zip),
                    InputStream = stream,
                    ContentType = "application/octet-stream"
                };
                await transferUtility.UploadAsync(request, token);
            }
            catch (AmazonS3Exception e)
            {
                Console.WriteLine("Error encountered on server. Message:'{0}' when writing an object", e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unknown encountered on server. Message:'{0}' when writing an object", e.Message);
            }
        }

        public async Task<byte[]?> GetZipArchiveInBytesFromS3Async(Identifier identifier, CancellationToken cancellationToken = default)
        {
            var obj = await _amazonS3.GetObjectAsync(_options.BucketName, identifier.GetValue(ZipKey.S3Zip), cancellationToken);
            if (obj == null)
            {
                return null;
            }

            using var destStream = new MemoryStream();
            await using var stream = obj.ResponseStream;
            await stream.CopyToAsync(destStream, cancellationToken);
            return destStream.ToArray();
        }

    }
}
