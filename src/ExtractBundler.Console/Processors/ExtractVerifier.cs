namespace ExtractBundler.Console.Processors;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Be.Vlaanderen.Basisregisters.GrAr.Notifications;
using CloudStorageClients;
using Microsoft.Extensions.Logging;

public class ExtractVerifier
{
    private readonly ILogger<ExtractVerifier> _logger;
    private readonly S3Client _s3Client;
    private readonly AzureBlobClient _azureBlobClient;
    private readonly INotificationService _notificationService;

    public ExtractVerifier(
        S3Client s3Client,
        AzureBlobClient azureBlobClient,
        INotificationService notificationService,
        ILoggerFactory loggerFactory)
    {
        _s3Client = s3Client;
        _azureBlobClient = azureBlobClient;
        _notificationService = notificationService;
        _logger = loggerFactory.CreateLogger<ExtractVerifier>();
    }

    public async Task Verify(CancellationToken cancellationToken)
    {
        var notifications = new List<string>();
        var blobs= (await _azureBlobClient.ListBlobsAsync(cancellationToken)).ToList();
        foreach (var identifier in Enum.GetValues<Identifier>())
        {
            var s3BlobExists = await _s3Client.DoesBlobExists(identifier, cancellationToken);
            if (!s3BlobExists)
            {
                var message = $"S3 blob {identifier.GetValue(ZipKey.S3Zip)} does not exist.";
                _logger.LogWarning(message);
                notifications.Add(message);
            }

            var azureBlobExists = blobs.Any(x =>
                x.Item1.EndsWith(identifier.GetValue(ZipKey.AzureZip))
                && x.Item4.HasValue
                && x.Item4.Value.Date == DateTime.Today);

            if (!azureBlobExists)
            {
                var message = $"Azure blob {identifier.GetValue(ZipKey.AzureZip)} does not exist.";
                _logger.LogWarning(message);
                notifications.Add(message);
            }
        }

        if (notifications.Count > 0)
        {
            var message = string.Join(Environment.NewLine, notifications);
            await _notificationService.PublishToTopicAsync(
                new NotificationMessage("ExtractBundler",
                    message,
                    "Extract Bundler",
                    NotificationSeverity.Danger));
        }
    }
}
