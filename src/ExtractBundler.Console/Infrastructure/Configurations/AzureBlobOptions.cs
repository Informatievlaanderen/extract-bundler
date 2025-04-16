namespace ExtractBundler.Console.Infrastructure.Configurations;

public class AzureBlobOptions
{
    public required string BaseUrl { get; set; }
    public required string TenantId { get; set; }
    public required string ClientKey { get; set; }
    public required string ClientSecret { get; set; }
    public required string ContainerName { get; set; }
    public required string ConnectionString { get; set; }
    public bool IsTest { get; set; }
    public bool IsAzurite { get; set; }
    public bool Enabled { get; set; }
}
