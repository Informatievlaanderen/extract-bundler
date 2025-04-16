namespace ExtractBundler.Console.Infrastructure.Configurations;

public class S3Options
{
    public required string AccessKey { get; set; }
    public required string AccessSecret { get; set; }
    public required string Region { get; set; }
    public required string BucketName { get; set; }
    public bool IsMinio { get; set; }
    public required string BaseUrl { get; set; }
}
