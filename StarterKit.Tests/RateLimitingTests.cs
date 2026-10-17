using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;

namespace StarterKit.Tests;

public class RateLimitedApiFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "3");
        builder.UseSetting("RateLimiting:Auth:WindowSeconds", "60");
    }
}

public class RateLimitingTests(RateLimitedApiFactory factory) : IClassFixture<RateLimitedApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_is_rate_limited_after_too_many_attempts()
    {
        var body = new { email = "nobody@test.com", password = "Wrong1234!" };

        for (var i = 0; i < 3; i++)
        {
            var allowed = await _client.PostAsJsonAsync("/api/auth/login", body);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var blocked = await _client.PostAsJsonAsync("/api/auth/login", body);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter);
    }

    [Fact]
    public async Task Endpoints_without_the_policy_are_not_limited()
    {
        for (var i = 0; i < 10; i++)
        {
            var response = await _client.GetAsync("/health/live");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}