namespace ExtractBundler.IntegrationTests
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Be.Vlaanderen.Basisregisters.DockerUtilities;
    using Ductus.FluentDocker.Services;
    using Infrastructure;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Xunit;

    public class IntegrationTestFixture : IAsyncLifetime
    {
        private ICompositeService? _dockerCompose;
        public TestServer TestServer { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            //_dockerCompose = DockerComposer.Compose("docker-compose.yml", "extract-bundler-integration-tests");
            await WaitForContainerToBecomeAvailable();

            var hostBuilder = new WebHostBuilder()
                .ConfigureAppConfiguration((hostContext, builder) =>
                {
                    var env = hostContext.HostingEnvironment;
                    builder
                        .SetBasePath(env.ContentRootPath)
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                        .AddJsonFile($"appsettings.{env.EnvironmentName.ToLowerInvariant()}.json",
                            optional: true,
                            reloadOnChange: false)
                        .AddJsonFile($"appsettings.{Environment.MachineName.ToLowerInvariant()}.json",
                            optional: true,
                            reloadOnChange: false)
                        .AddEnvironmentVariables()
                        .Build();
                })
                .ConfigureLogging(loggingBuilder => loggingBuilder.AddConsole())
                .UseStartup<TestStartup>()
                .UseTestServer();

            TestServer = new TestServer(hostBuilder);
        }

        public async Task DisposeAsync()
        {
            await Task.Run(() => _dockerCompose?.Stop());
        }

        private static async Task WaitForContainerToBecomeAvailable()
        {
            foreach (var _ in Enumerable.Range(0, 60))
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(new Uri("http://localhost:19404/ping"));
                if (response.IsSuccessStatusCode)
                {
                    break;
                }
                Console.WriteLine("Waiting for docker container to be alive.");
            }
        }
    }
}
