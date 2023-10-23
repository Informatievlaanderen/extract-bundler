namespace ExtractBundler.Console.Infrastructure.Configurations;

using System;
using System.Collections.Generic;

public class BundlerOptions
{
    public BundlerOptionItem StreetName { get; set; }
    public BundlerOptionItem Address { get; set; }
    public BundlerOptionItem AddressLinks { get; set; }
    public BundlerOptionItem Full { get; set; }
}

public class BundlerOptionItem
{
    public string Urls { get; set; }
    public bool Enabled { get; set; }

    public IEnumerable<string> UrlsToList()
    {
        if (string.IsNullOrWhiteSpace(Urls))
        {
            throw new ArgumentNullException("Missing Urls in bundler");
        }
        return Urls.Split(",");
    }
}
