namespace ExtractBundler.IntegrationTests;

using System;
using System.Threading.Tasks;
using Console;

public class FakeTokenProvider : ITokenProvider
{
    public Task<string> GetAccessToken()
    {
        return Task.FromResult(Guid.NewGuid().ToString("D"));
    }
}
