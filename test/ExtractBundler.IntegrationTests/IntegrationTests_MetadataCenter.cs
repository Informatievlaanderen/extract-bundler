namespace ExtractBundler.IntegrationTests;

using System;
using System.Threading.Tasks;
using Console.HttpClients;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public partial class IntegrationTests
{
    [Fact]
    public async Task ClientBehaviorTests()
    {
        //Expected
        var expectedCswPublicationResponse =
            "<csw:TransactionResponse xmlns:csw=\"http://www.opengis.net/cat/csw/2.0.2\">\n  <csw:TransactionSummary>\n    <csw:totalInserted>0</csw:totalInserted>\n    <csw:totalUpdated>1</csw:totalUpdated>\n    <csw:totalDeleted>0</csw:totalDeleted>\n  </csw:TransactionSummary>\n</csw:TransactionResponse>";
       var expectedIdentifier = "72848a38-5db1-4705-8fb2-74e8353b1186";

        // Services
        var metaDataCenterHttpClient = _fixture.TestServer.Services.GetService<MetaDataCenterHttpClient>();

        // Test response
        var actualCswPublicationResponse =
            (await metaDataCenterHttpClient!.UpdateCswPublication(Identifier.StreetName, DateTime.Now))!.ToString();
        actualCswPublicationResponse = actualCswPublicationResponse.Replace("\r\n", "\n");
        var xmlMetaDataString = await metaDataCenterHttpClient.GetXmlAsString(Identifier.StreetName);

        // Results
        //Assert.True(string.Equals(expectedCswPublicationResponse, actualCswPublicationResponse, StringComparison.OrdinalIgnoreCase));

        actualCswPublicationResponse.Should().BeEquivalentTo(expectedCswPublicationResponse);
        xmlMetaDataString.Should().Contain(expectedIdentifier);
    }
}
