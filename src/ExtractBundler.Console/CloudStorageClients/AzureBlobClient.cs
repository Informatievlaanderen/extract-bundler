namespace ExtractBundler.Console.CloudStorageClients
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using Azure.Storage.Blobs.Specialized;
    using Infrastructure.Configurations;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public class AzureBlobClient
    {
        private readonly AzureBlobOptions _options;
        private const int ChunkSizeInBytes = 4 * 1024 * 1024; // 4 MB chunk size
        private readonly BlobContainerClient _containerClient;
        private readonly ILogger<AzureBlobClient> _logger;

        public AzureBlobClient(IOptions<AzureBlobOptions> options, BlobServiceClient client,
            ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<AzureBlobClient>();
            _options = options.Value;
            _containerClient = client.GetBlobContainerClient(_options.ContainerName);
            if (_options.IsAzurite)
            {
                _containerClient.CreateIfNotExists(PublicAccessType.BlobContainer);
            }
        }

        private string GetBlobName(Identifier identifier)
        {
            var isTest = _options.IsTest;
            var blobName = identifier.GetValue(ZipKey.AzureZip);
            if (identifier == Identifier.Full)
            {
                return isTest ? $"31086/{blobName}" : $"10142/{blobName}";
            }

            if (identifier == Identifier.StreetName)
            {
                return isTest ? $"31088/{blobName}" : $"10143/{blobName}";
            }

            if (identifier == Identifier.Address)
            {
                return isTest ? $"31087/{blobName}" : $"10145/{blobName}";
            }

            if (identifier == Identifier.AddressLinks)
            {
                return isTest ? $"31089/{blobName}" : $"10144/{blobName}";
            }

            return blobName;
        }

        public async Task UploadBlobInChunksAsync(
            byte[] content,
            Identifier identifier,
            CancellationToken cancellationToken = default)
        {
            BlockBlobClient blobClient = _containerClient.GetBlockBlobClient(GetBlobName(identifier));
            using var sourceStream = new MemoryStream(content);
            sourceStream.Seek(0, SeekOrigin.Begin);
            long remainingBytes = sourceStream.Length;
            long offset = 0;
            byte[] buffer = new byte[ChunkSizeInBytes];
            var blockIds = new List<string>();
            while (remainingBytes > 0)
            {
                int bytesRead = await sourceStream.ReadAsync(
                    buffer,
                    0,
                    (int)Math.Min(ChunkSizeInBytes, remainingBytes),
                    cancellationToken);

                using (MemoryStream ms = new MemoryStream(buffer, 0, bytesRead))
                {
                    var blockId = Convert.ToBase64String(BitConverter.GetBytes(offset));
                    blockIds.Add(blockId);
                    await blobClient.StageBlockAsync(blockId, ms, cancellationToken: cancellationToken);
                }

                remainingBytes -= bytesRead;
                offset += bytesRead;
            }

            var commitOptions = new CommitBlockListOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/octet-stream"
                }
            };
            await blobClient.CommitBlockListAsync(blockIds, commitOptions, cancellationToken);
        }

        public async Task<IEnumerable<Tuple<string, string, long?>>> ListBlobsAsync(
            CancellationToken cancellationToken = default)
        {
            var blobItems = new List<Tuple<string, string, long?>>();
            try
            {
                await foreach (var blobItem in _containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
                {
                    _logger.LogInformation($"Blob name: {blobItem.Name}, Blob type: {blobItem.Properties.BlobType}");
                    var name = blobItem.Name;
                    var type = blobItem.Properties.ContentType;
                    var size = blobItem.Properties.ContentLength;

                    blobItems.Add(Tuple.Create(name, type, size));
                }
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError($"Error listing blobs: {ex.Status}:{ex.ErrorCode} - {ex.Message}", ex);
            }

            return blobItems;
        }


        public async Task<byte[]?> DownloadBlobAsync(string blobName, CancellationToken cancellationToken = default)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(blobName);
                var response = await blobClient.DownloadContentAsync(cancellationToken);
                var zipAsBytes = response.Value.Content.ToArray();
                return zipAsBytes;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError($"Error listing blobs: {ex.Status}:{ex.ErrorCode} - {ex.Message}", ex);
            }

            return null;
        }
    }
}
