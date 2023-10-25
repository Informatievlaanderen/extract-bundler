namespace ExtractBundler.IntegrationTests;

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public partial class IntegrationTests
{
    [Fact]
    public async Task AddToZipArchiveTest()
    {
        var zipBytes = await GetZipInBytes();
        Assert.NotNull(zipBytes);
        await ValidateZipArchive(zipBytes);
    }

    [Fact]
    public async Task CopyFromZipArchiveToAnotherZipArchiveAsyncTest()
    {
        var zipBytes = await GetZipInBytes();
        await using var sourceZipStream = new MemoryStream(zipBytes);
        using var sourceZipArchive = new ZipArchive(sourceZipStream, ZipArchiveMode.Read);
        var sourceEntries = sourceZipArchive.Entries;

        byte[] destinationZipStreamBytes;
        await using (var destinationZipStream = new MemoryStream())
        {
            //Write mode
            using (var destinationZipArchive = new ZipArchive(destinationZipStream, ZipArchiveMode.Create))
            {
                foreach (var srcEntry in sourceEntries)
                {
                    //Run Test
                    await srcEntry.CopyToAsync(destinationZipArchive);
                }
            }
            destinationZipStreamBytes = destinationZipStream.ToArray();
        }

        Assert.NotNull(destinationZipStreamBytes);
        await ValidateZipArchive(zipBytes);
    }

    private async Task<byte[]> GetZipInBytes(CancellationToken cancellationToken = default)
    {
        var pdfName = "Meta_GRARStraatnamen.pdf";
        await using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            await zip.AddToZipArchive(pdfName, _pdfContent, cancellationToken);
        }
        var zipInBytes = zipStream.ToArray();
        return zipInBytes;
    }

    private async Task ValidateZipArchive(byte[] zipArchiveInBytes)
    {
        var expectedName = "Meta_GRARStraatnamen.pdf";
        await using var sourceStream = new MemoryStream(zipArchiveInBytes);

        using var destinationZipArchive = new ZipArchive(sourceStream, ZipArchiveMode.Read);
        var destEntries = destinationZipArchive.Entries;
        var destEntry = destEntries.FirstOrDefault(i => i.Name == expectedName);

        //Check if zip contains pdf file
        Assert.True(destEntries.Count == 1);
        Assert.NotNull(destEntry);

        //Read file and check if it's still the same
        await using var src = destEntry!.Open();
        await using var dest = new MemoryStream();
        await src.CopyToAsync(dest);
        var actualPdfContent = dest.ToArray();

        Assert.Equal(_pdfContent, actualPdfContent);
    }
}
