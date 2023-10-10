namespace ExtractBundler;

using System;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public static class FileExtensions
{
    public static async Task CopyToAsync(
        this ZipArchiveEntry? source,
        ZipArchive destinationZipArchive,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        if (source == null)
        {
            return;
        }

        var destination = destinationZipArchive!.CreateEntry(fileName ?? source.FullName, CompressionLevel.Fastest);
        await using var sourceEntryStream = source.Open();
        await using var destEntryStream = destination.Open();
        await sourceEntryStream.CopyToAsync(destEntryStream, cancellationToken);
    }

    public static async Task AddToZipArchive(
        this ZipArchive archive,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        var entry = archive.CreateEntry(fileName, CompressionLevel.Fastest);
        await using var entryStream = entry.Open();
        await entryStream.WriteAsync(content, cancellationToken);
    }

    public static async Task AddToZipArchive(
        this ZipArchive archive,
        string fileName,
        string content,
        CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        await archive.AddToZipArchive(fileName, bytes, cancellationToken);
    }

    public static string FormatBytes(this long byteSize)
    {
        var b = (double)byteSize;
        if (b < 1024)
        {
            return $"{byteSize} B";
        }

        if (b / 1024 < 1024)
        {
            return $"{Math.Round(b / 1024, 2)} KB";
        }

        if (b / Math.Pow(1024, 2) < 1024)
        {
            return $"{Math.Round(b / Math.Pow(1024, 2), 2)} MB";
        }

        if (b / Math.Pow(1024, 3) < 1024)
        {
            return $"{Math.Round(b / Math.Pow(1024, 3), 2)} GB";
        }

        return $"{Math.Round(b / Math.Pow(1024, 4), 2)} TB";
    }
}
