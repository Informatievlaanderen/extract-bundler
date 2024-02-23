namespace ExtractBundler.Console.Infrastructure.Configurations;

public class MetadataCenterOptions
{
    public string FullIdentifier { get; set; }
    public string StreetNameIdentifier { get; set; }
    public string AddressIdentifier { get; set; }
    public string AddressLinksIdentifier { get; set; }
    public string BaseUrl { get; set; }

    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string TokenEndPoint { get; set; }
}
