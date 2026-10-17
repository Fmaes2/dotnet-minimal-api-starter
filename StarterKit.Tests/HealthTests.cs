using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace StarterKit.Tests
{
    public class HealthTests(ApiFactory factory) : IClassFixture<ApiFactory>
    {
        [Fact]
        public async Task Live_returns_200()
        {
            var response = await factory.CreateClient().GetAsync("/health/live");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Ready_returns_200_when_the_database_is_reachable()
        {
            var response = await factory.CreateClient().GetAsync("/health/ready");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Ready_returns_503_when_a_check_fails()
        {
            var client = factory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                    services.AddHealthChecks().AddCheck(
                        "always-failing",
                        () => HealthCheckResult.Unhealthy(),
                        tags: ["ready"])))
                .CreateClient();

            var response = await client.GetAsync("/health/ready");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
    }
}
