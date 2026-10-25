using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using StarterKit.Api.Endpoints;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace StarterKit.Tests;

public class RefreshTokenTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Password = "Test1234!";

    [Fact]
    public async Task Login_ReturnsAccessAndRefreshTokens()
    {
        var client = factory.CreateClient();

        var auth = await RegisterAndLoginAsync(client);

        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));
        Assert.True(auth.RefreshTokenExpiresAt > auth.AccessTokenExpiresAt);
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewWorkingPair()
    {
        var client = factory.CreateClient();
        var login = await RegisterAndLoginAsync(client);

        var response = await RefreshAsync(client, login.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(refreshed);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithAlreadyUsedToken_Returns401()
    {
        var client = factory.CreateClient();
        var login = await RegisterAndLoginAsync(client);
        (await RefreshAsync(client, login.RefreshToken)).EnsureSuccessStatusCode();

        var reuse = await RefreshAsync(client, login.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReusingRotatedTokenAfterLeeway_RevokesWholeFamily()
    {
        var (app, clock) = CreateAppWithFakeClock();
        using var _ = app;
        var client = app.CreateClient();
        var login = await RegisterAndLoginAsync(client);
        var first = await RefreshAsync(client, login.RefreshToken);
        var rotated = (await first.Content.ReadFromJsonAsync<AuthResponse>())!;

        clock.Advance(TimeSpan.FromSeconds(11));
        var reuse = await RefreshAsync(client, login.RefreshToken);
        var afterReuse = await RefreshAsync(client, rotated.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await RefreshAsync(client, "not-a-real-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithEmptyToken_Returns400()
    {
        var client = factory.CreateClient();

        var response = await RefreshAsync(client, "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_Returns401()
    {
        var (app, clock) = CreateAppWithFakeClock();
        using var _ = app;
        var client = app.CreateClient();
        var login = await RegisterAndLoginAsync(client);

        clock.Advance(TimeSpan.FromDays(8));
        var response = await RefreshAsync(client, login.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var client = factory.CreateClient();
        var login = await RegisterAndLoginAsync(client);

        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = login.RefreshToken });
        var refresh = await RefreshAsync(client, login.RefreshToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_WithUnknownToken_Returns204()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task LogoutAll_RevokesEverySession()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        var sessionA = await RegisterAndLoginAsync(client, email);
        var sessionB = await LoginAsync(client, email);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionA.AccessToken);
        var logoutAll = await client.PostAsync("/api/auth/logout-all", null);

        Assert.Equal(HttpStatusCode.NoContent, logoutAll.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, sessionA.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, sessionB.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task LogoutAll_WithoutAccessToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/auth/logout-all", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReusingRotatedTokenWithinLeeway_KeepsFamily()
    {
        var (app, clock) = CreateAppWithFakeClock();
        using var _ = app;
        var client = app.CreateClient();
        var login = await RegisterAndLoginAsync(client);
        var first = await RefreshAsync(client, login.RefreshToken);
        var rotated = (await first.Content.ReadFromJsonAsync<AuthResponse>())!;

        clock.Advance(TimeSpan.FromSeconds(9));
        var reuse = await RefreshAsync(client, login.RefreshToken);
        var next = await RefreshAsync(client, rotated.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";

    private static async Task<AuthResponse> RegisterAndLoginAsync(HttpClient client, string? email = null)
    {
        email ??= NewEmail();
        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
        register.EnsureSuccessStatusCode();
        return await LoginAsync(client, email);
    }

    private static async Task<AuthResponse> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string token) =>
        client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = token });

    private (WebApplicationFactory<Program> App, FakeTimeProvider Clock) CreateAppWithFakeClock()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var app = factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(clock)));
        return (app, clock);
    }
}