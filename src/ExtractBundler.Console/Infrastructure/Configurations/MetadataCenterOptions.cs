namespace ExtractBundler.Console.Infrastructure.Configurations;

public class MetadataCenterOptions
{
    public required string FullIdentifier { get; set; }
    public required string StreetNameIdentifier { get; set; }
    public required string AddressIdentifier { get; set; }
    public required string AddressLinksIdentifier { get; set; }
    public required string BaseUrl { get; set; }

    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
    public required string TokenEndPoint { get; set; }
}
