namespace ExtractBundler.Console.Infrastructure.Configurations;

using System;
using System.Collections.Generic;

public class BundlerOptions
{
    public required BundlerOptionItem StreetName { get; set; }
    public required BundlerOptionItem Address { get; set; }
    public required BundlerOptionItem AddressLinks { get; set; }
    public required BundlerOptionItem Full { get; set; }
}

public class BundlerOptionItem
{
    public required string Urls { get; set; }
    public bool Enabled { get; set; }
    public bool GeopackageEnabled { get; set; }

    public IEnumerable<string> UrlsToList()
    {
        if (string.IsNullOrWhiteSpace(Urls))
        {
            throw new ArgumentException("Missing Urls in bundler");
        }
        return Urls.Split(",");
    }
}
