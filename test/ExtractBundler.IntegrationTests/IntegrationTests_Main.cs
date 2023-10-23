namespace ExtractBundler.IntegrationTests;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Autofac.Core;
using Console.Bundlers;
using Console.CloudStorageClients;
using Console.Infrastructure.Configurations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

public partial class IntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;
    private readonly byte[] _pdfContent;
    private readonly S3Client _s3Client;
    private readonly AzureBlobClient _azureBlobClient;
    private readonly AzureBlobOptions _azureOptions;
    private readonly AddressBundler addressBundler;
    private readonly AddressLinksBundler addressLinksBundler;
    private readonly StreetNameBundler streetNameBundler;
    private readonly FullBundler fullBundler;


    public IntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _pdfContent = Convert.FromBase64String(
            "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VHlwZS9QYWdlL01lZGlhQm94WzAgMCA2MTIgNzkyXS9SZXNvdXJjZXM8PC9Gb250PDwvRjEgMiAwIFIvRjIgMyAwIFI+Pj4+L0NvbnRlbnRzIDEwIDAgUi9QYXJlbnQgNSAwIFI+PgplbmRvYmoKMiAwIG9iago8PC9UeXBlL0ZvbnQvU3VidHlwZS9UeXBlMS9CYXNlRm9udC9UaW1lcy1Cb2xkL0VuY29kaW5nL1dpbkFuc2lFbmNvZGluZz4+CmVuZG9iagozIDAgb2JqCjw8L1R5cGUvRm9udC9TdWJ0eXBlL1R5cGUxL0Jhc2VGb250L1RpbWVzLVJvbWFuL0VuY29kaW5nL1dpbkFuc2lFbmNvZGluZz4+CmVuZG9iago1IDAgb2JqCjw8L1R5cGUvUGFnZXMvQ291bnQgNC9LaWRzWzEgMCBSIDcgMCBSIDkgMCBSIDExIDAgUl0+PgplbmRvYmoKMTIgMCBvYmoKPDwvVHlwZS9DYXRhbG9nL1BhZ2VzIDUgMCBSPj4KZW5kb2JqCjEzIDAgb2JqCjw8L1Byb2R1Y2VyKGlUZXh0riA1LjUuMTMgqTIwMDAtMjAxOCBpVGV4dCBHcm91cCBOViBcKEFHUEwtdmVyc2lvblwpKS9DcmVhdGlvbkRhdGUoRDoyMDIzMDkyMDEzMTE1NCswMicwMCcpL01vZERhdGUoRDoyMDIzMDkyMDEzMTE1NCswMicwMCcpL1RpdGxlKEdlYm91d2VuLSBlbiBhZHJlc3NlbnJlZ2lzdGVyIC0gc3RyYWF0bmFtZW4pPj4KZW5kb2JqCnhyZWYKMCAxNAowMDAwMDAwMDAwIDY1NTM1IGYgCjAwMDAwMDE4MzAgMDAwMDAgbiAKMDAwMDAwNzMxMSAwMDAwMCBuIAowMDAwMDA3NDAwIDAwMDAwIG4gCjAwMDAwMDAwMTUgMDAwMDAgbiAKMDAwMDAwNzQ5MCAwMDAwMCBuIAowMDAwMDAxOTUxIDAwMDAwIG4gCjAwMDAwMDM3MTIgMDAwMDAgbiAKMDAwMDAwMzgzMyAwMDAwMCBuIAowMDAwMDA1NjY0IDAwMDAwIG4gCjAwMDAwMDU3ODUgMDAwMDAgbiAKMDAwMDAwNzE4OCAwMDAwMCBuIAowMDAwMDA3NTYwIDAwMDAwIG4gCjAwMDAwMDc2MDYgMDAwMDAgbiAKdHJhaWxlcgo8PC9TaXplIDE0L1Jvb3QgMTIgMCBSL0luZm8gMTMgMCBSL0lEIFs8MjA1NDhjM2JjZjNlNDQ3YjZjODZmNDA3MDU4YjFmZGE+PDIwNTQ4YzNiY2YzZTQ0N2I2Yzg2ZjQwNzA1OGIxZmRhPl0+PgolaVRleHQtNS41LjEzCnN0YXJ0eHJlZgo3ODE2CiUlRU9GCg==");
        _s3Client = _fixture.TestServer.Services.GetService<S3Client>()!;
        _azureBlobClient = _fixture.TestServer.Services.GetService<AzureBlobClient>()!;
        _azureOptions = _fixture.TestServer.Services.GetService<IOptions<AzureBlobOptions>>()!.Value;
        addressBundler = _fixture.TestServer.Services.GetService<AddressBundler>()!;
        addressLinksBundler = _fixture.TestServer.Services.GetService<AddressLinksBundler>()!;
        streetNameBundler = _fixture.TestServer.Services.GetService<StreetNameBundler>()!;
        fullBundler = _fixture.TestServer.Services.GetService<FullBundler>()!;
    }

    [Fact]
    private async Task Bundler_Should_Upload_To_S3_And_Azure()
    {
        //Act
        await Task.WhenAll(streetNameBundler.Start(), addressBundler.Start(), addressLinksBundler.Start(), fullBundler.Start());

        //Assert

        await Assert_AddressBundler_Should_UploadToS3AndAzure();
        await Assert_AddressLinksBundler_Should_UploadToS3AndAzure();
        await Assert_StreetNameBundler_Should_UploadToS3AndAzure();
        await Assert_FullBundler_Should_UploadToS3AndAzure();
    }

    private async Task Assert_FullBundler_Should_UploadToS3AndAzure()
    {
        // Azure (Azurite) Download
        var list = await _azureBlobClient.ListBlobsAsync();
        var expectedBlobName = _azureOptions.IsTest ? "31086/GRAR.zip" : "10142/GRAR.zip";
        var blobName = list.FirstOrDefault(i => i.Item1 == expectedBlobName)?.Item1;
        Assert.NotNull(blobName);
        blobName.Should().NotBeNull();

        var azureZipAsBytes = await _azureBlobClient.DownloadBlobAsync(blobName!);
        azureZipAsBytes.Should().NotBeNull();
        using var azureZipStream = new MemoryStream(azureZipAsBytes!);

        var dateStamp = DateTime.Today.ToString("yyyyMMdd");

        var azureExpectedFiles = new[]
        {
            OsAgnosticPath($"{dateStamp}_GRAR_Data/dBASE/AdresPerceelKoppelingen.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/dBASE/AdresPerceelKoppelingen_metadata.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/dBASE/AdresGebouweenheidKoppelingen.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/dBASE/AdresGebouweenheidKoppelingen_metadata.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/dBASE/CrabHuisnummer.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/dBASE/CrabSubadres.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/dBASE/Gemeente.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/dBASE/Gemeente_metadata.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/dBASE/Perceel.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/dBASE/Perceel_metadata.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/dBASE/Postinfo.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/dBASE/Postinfo_metadata.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/dBASE/Straatnaam.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/dBASE/Straatnaam_metadata.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/Shapefile/Adres.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/Shapefile/Adres.prj"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/Shapefile/Adres.shp"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/Shapefile/Adres.shx"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/Shapefile/Adres_metadata.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/Shapefile/Gebouw.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/Shapefile/Gebouw.prj"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/Shapefile/Gebouw.shp"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/Shapefile/Gebouw.shx"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/Shapefile/Gebouw_metadata.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/Shapefile/Gebouweenheid.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/Shapefile/Gebouweenheid.prj"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/Shapefile/Gebouweenheid.shp"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/Shapefile/Gebouweenheid.shx"),
            OsAgnosticPath($"{dateStamp}_GRAR_Data/Shapefile/Gebouweenheid_metadata.dbf"),
            "HandleidingZipPakketten.pdf",
            "Meta_GRAR.pdf",
            "Meta_GRAR.xml"
        }.ToList();
        using var azureZipArchive = new ZipArchive(azureZipStream, ZipArchiveMode.Read);
        var azureActualFiles = azureZipArchive.Entries.Select(i => i.FullName);
        azureExpectedFiles.Should().BeEquivalentTo(azureActualFiles);

        // S3 (Minio) Download
        var s3ZipAsBytes = await _s3Client.GetZipArchiveInBytesFromS3Async(Identifier.Full);
        s3ZipAsBytes.Should().NotBeNull();
        using var s3ZipStream = new MemoryStream(s3ZipAsBytes!);
        using var s3ZipArchive = new ZipArchive(s3ZipStream, ZipArchiveMode.Read);
        var s3ActualFiles = s3ZipArchive.Entries.Select(i => i.FullName);
        var s3ExpectedFiles = new[]
        {
            "AdresPerceelKoppelingen.dbf",
            "AdresPerceelKoppelingen_metadata.dbf",
            "AdresGebouweenheidKoppelingen.dbf",
            "AdresGebouweenheidKoppelingen_metadata.dbf",
            "CrabHuisnummer.dbf",
            "CrabSubadres.dbf",
            "Gemeente.dbf",
            "Gemeente_metadata.dbf",
            "Perceel.dbf",
            "Perceel_metadata.dbf",
            "Postinfo.dbf",
            "Postinfo_metadata.dbf",
            "Straatnaam.dbf",
            "Straatnaam_metadata.dbf",
            "Adres.dbf",
            "Adres.prj",
            "Adres.shp",
            "Adres.shx",
            "Adres_metadata.dbf",
            "Gebouw.dbf",
            "Gebouw.prj",
            "Gebouw.shp",
            "Gebouw.shx",
            "Gebouw_metadata.dbf",
            "Gebouweenheid.dbf",
            "Gebouweenheid.prj",
            "Gebouweenheid.shp",
            "Gebouweenheid.shx",
            "Gebouweenheid_metadata.dbf"
        }.ToList();
        s3ExpectedFiles.Should().BeEquivalentTo(s3ActualFiles);
    }

    private async Task Assert_AddressBundler_Should_UploadToS3AndAzure()
    {
        // Azure (Azurite) Download
        var list = await _azureBlobClient.ListBlobsAsync();
        var expectedBlobName = _azureOptions.IsTest ? "31087/GRAR_Adressen.zip" : "10143/GRAR_Adressen.zip";
        var blobName = list.FirstOrDefault(i => i.Item1 == expectedBlobName)?.Item1;
        blobName.Should().NotBeNull();

        var azureZipAsBytes = await _azureBlobClient.DownloadBlobAsync(blobName!);
        azureZipAsBytes.Should().NotBeNull();
        using var azureZipStream = new MemoryStream(azureZipAsBytes!);

        var dateStamp = DateTime.Today.ToString("yyyyMMdd");

        var azureExpectedFiles = new[]
        {
            OsAgnosticPath($"{dateStamp}_GRAR_Adressen_Data/Shapefile/Adres.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Adressen_Data/Shapefile/Adres.prj"),
            OsAgnosticPath($"{dateStamp}_GRAR_Adressen_Data/Shapefile/Adres.shp"),
            OsAgnosticPath($"{dateStamp}_GRAR_Adressen_Data/Shapefile/Adres.shx"),
            OsAgnosticPath($"{dateStamp}_GRAR_Adressen_Data/Shapefile/Adres_metadata.dbf"),
            "HandleidingZipPakketten.pdf",
            "Meta_GRARAdressen.pdf",
            "Meta_GRARAdressen.xml"
        }.ToList();
        using var azureZipArchive = new ZipArchive(azureZipStream, ZipArchiveMode.Read);
        var azureActualFiles = azureZipArchive.Entries.Select(i => i.FullName);
        azureExpectedFiles.Should().BeEquivalentTo(azureActualFiles);

        // S3 (Minio) Download
        var s3ZipAsBytes = await _s3Client.GetZipArchiveInBytesFromS3Async(Identifier.Address);
        s3ZipAsBytes.Should().NotBeNull();
        using var s3ZipStream = new MemoryStream(s3ZipAsBytes!);
        using var s3ZipArchive = new ZipArchive(s3ZipStream, ZipArchiveMode.Read);
        var s3ActualFiles = s3ZipArchive.Entries.Select(i => i.FullName);
        var s3ExpectedFiles = new[]
        {
            "Adres.dbf",
            "Adres.prj",
            "Adres.shp",
            "Adres.shx",
            "Adres_metadata.dbf"
        }.ToList();
        s3ExpectedFiles.Should().BeEquivalentTo(s3ActualFiles);
    }

    private async Task Assert_AddressLinksBundler_Should_UploadToS3AndAzure()
    {
        // Azure (Azurite) Download
        var list = await _azureBlobClient.ListBlobsAsync();
        var expectedBlobName =
            _azureOptions.IsTest ? "31089/GRAR_Adreskoppelingen.zip" : "10144/GRAR_Adreskoppelingen.zip";
        var blobName = list.FirstOrDefault(i => i.Item1 == expectedBlobName)?.Item1;
        blobName.Should().NotBeNull();

        var azureZipAsBytes = await _azureBlobClient.DownloadBlobAsync(blobName!);
        azureZipAsBytes.Should().NotBeNull();
        using var azureZipStream = new MemoryStream(azureZipAsBytes!);

        var dateStamp = DateTime.Today.ToString("yyyyMMdd");
        var azureExpectedFiles = new[]
        {
            OsAgnosticPath($"{dateStamp}_GRAR_Adreskoppelingen_Data/dBASE/AdresPerceelKoppelingen.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Adreskoppelingen_Data/dBASE/AdresPerceelKoppelingen_metadata.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Adreskoppelingen_Data/dBASE/AdresGebouweenheidKoppelingen.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Adreskoppelingen_Data/dBASE/AdresGebouweenheidKoppelingen_metadata.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Adreskoppelingen_Data/Shapefile/Adres.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Adreskoppelingen_Data/Shapefile/Adres.prj"),
            OsAgnosticPath($"{dateStamp}_GRAR_Adreskoppelingen_Data/Shapefile/Adres.shp"),
            OsAgnosticPath($"{dateStamp}_GRAR_Adreskoppelingen_Data/Shapefile/Adres.shx"),
            OsAgnosticPath($"{dateStamp}_GRAR_Adreskoppelingen_Data/Shapefile/Adres_metadata.dbf"),
            "HandleidingZipPakketten.pdf",
            "Meta_GRARAdreskoppelingen.pdf",
            "Meta_GRARAdreskoppelingen.xml"
        }.ToList();
        using var azureZipArchive = new ZipArchive(azureZipStream, ZipArchiveMode.Read);
        var azureActualFiles = azureZipArchive.Entries.Select(i => i.FullName);
        azureExpectedFiles.Should().BeEquivalentTo(azureActualFiles);

        // S3 (Minio) Download
        var s3ZipAsBytes = await _s3Client.GetZipArchiveInBytesFromS3Async(Identifier.AddressLinks);
        s3ZipAsBytes.Should().NotBeNull();
        using var s3ZipStream = new MemoryStream(s3ZipAsBytes!);
        using var s3ZipArchive = new ZipArchive(s3ZipStream, ZipArchiveMode.Read);
        var s3ActualFiles = s3ZipArchive.Entries.Select(i => i.FullName);
        var s3ExpectedFiles = new[]
        {
            "AdresGebouweenheidKoppelingen.dbf",
            "AdresPerceelKoppelingen.dbf",
            "AdresGebouweenheidKoppelingen_metadata.dbf",
            "AdresPerceelKoppelingen_metadata.dbf",
            "Adres.dbf",
            "Adres.prj",
            "Adres.shp",
            "Adres.shx",
            "Adres_metadata.dbf"
        }.ToList();
        s3ExpectedFiles.Should().BeEquivalentTo(s3ActualFiles, o => o.WithoutStrictOrdering());
    }

    private async Task Assert_StreetNameBundler_Should_UploadToS3AndAzure()
    {
        // Azure (Azurite) Download
        var list = await _azureBlobClient.ListBlobsAsync();
        var expectedBlobName = _azureOptions.IsTest ? "31088/GRAR_Straatnamen.zip" : "10143/GRAR_Straatnamen.zip";
        var blobName = list.FirstOrDefault(i => i.Item1 == expectedBlobName)?.Item1;
        blobName.Should().NotBeNull();
        var azureZipAsBytes = await _azureBlobClient.DownloadBlobAsync(blobName!);
        azureZipAsBytes.Should().NotBeNull();
        using var azureZipStream = new MemoryStream(azureZipAsBytes!);
        var dateStamp = DateTime.Today.ToString("yyyyMMdd");
        var azureExpectedFiles = new[]
        {
            OsAgnosticPath($"{dateStamp}_GRAR_Straatnamen_Data/dBASE/Gemeente.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Straatnamen_Data/dBASE/Gemeente_metadata.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Straatnamen_Data/dBASE/Postinfo.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Straatnamen_Data/dBASE/Postinfo_metadata.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Straatnamen_Data/dBASE/Straatnaam.dbf"),
            OsAgnosticPath($"{dateStamp}_GRAR_Straatnamen_Data/dBASE/Straatnaam_metadata.dbf"),
            "HandleidingZipPakketten.pdf",
            "Meta_GRARStraatnamen.pdf",
            "Meta_GRARStraatnamen.xml"
        }.ToList();
        using var azureZipArchive = new ZipArchive(azureZipStream, ZipArchiveMode.Read);
        var azureActualFiles = azureZipArchive.Entries.Select(i => i.FullName);
        azureExpectedFiles.Should().BeEquivalentTo(azureActualFiles);

        // S3 (Minio) Download
        var s3ZipAsBytes = await _s3Client.GetZipArchiveInBytesFromS3Async(Identifier.StreetName);
        s3ZipAsBytes.Should().NotBeNull();
        using var s3ZipStream = new MemoryStream(s3ZipAsBytes!);
        using var s3ZipArchive = new ZipArchive(s3ZipStream, ZipArchiveMode.Read);
        var s3ActualFiles = s3ZipArchive.Entries.Select(i => i.FullName);
        var s3ExpectedFiles = new[]
        {
            "Gemeente.dbf",
            "Gemeente_metadata.dbf",
            "Postinfo.dbf",
            "Postinfo_metadata.dbf",
            "Straatnaam.dbf",
            "Straatnaam_metadata.dbf",
        }.ToList();
        s3ExpectedFiles.Should().BeEquivalentTo(s3ActualFiles);
    }

    private string OsAgnosticPath(string path) => Path.Combine(path.Split('/'));
}
