namespace ExtractBundler.Console.Infrastructure.Configurations;

public class S3Options
{
    public string AccessKey { get; set; }
    public string AccessSecret { get; set; }
    public string Region { get; set; }
    public string BucketName { get; set; }
    public bool IsMinio { get; set; }
    public string BaseUrl { get; set; }
}
