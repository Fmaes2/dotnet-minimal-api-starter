using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using StarterKit.Api.Auth;
using StarterKit.Api.Middleware;
using StarterKit.Api.Validation;
using System.Security.Claims;


namespace StarterKit.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", Register)
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy);

        group.MapPost("/login", Login)
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy);

        group.MapGet("/me", Me).RequireAuthorization();

        group.MapPost("/refresh", Refresh)
            .AddEndpointFilter<ValidationFilter<RefreshTokenRequest>>()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy);

        group.MapPost("/logout", Logout)
            .AddEndpointFilter<ValidationFilter<RefreshTokenRequest>>();

        group.MapPost("/logout-all", LogoutAll).RequireAuthorization();

        return app;
    }

    private static async Task<Results<NoContent, ValidationProblem>> Register(
        RegisterRequest req, UserManager<IdentityUser> users)
    {
        var user = new IdentityUser { UserName = req.Email, Email = req.Email };
        var result = await users.CreateAsync(user, req.Password);

        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(e => e.Code)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
            return TypedResults.ValidationProblem(errors);
        }

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> Login(
        LoginRequest req, UserManager<IdentityUser> users, TokenService tokens,
        RefreshTokenService refreshTokens, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(req.Email);
        if (user is null || !await users.CheckPasswordAsync(user, req.Password))
            return TypedResults.Unauthorized();

        var (access, accessExpires) = tokens.CreateToken(user);
        var refresh = await refreshTokens.CreateAsync(user.Id, ct);

        return TypedResults.Ok(new AuthResponse(access, accessExpires, refresh.Token, refresh.ExpiresAt));
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> Refresh(
        RefreshTokenRequest req, UserManager<IdentityUser> users, TokenService tokens,
        RefreshTokenService refreshTokens, CancellationToken ct)
    {
        var rotated = await refreshTokens.RotateAsync(req.RefreshToken, ct);
        if (rotated is null)
            return TypedResults.Unauthorized();

        var user = await users.FindByIdAsync(rotated.UserId);
        if (user is null)
            return TypedResults.Unauthorized();

        var (access, accessExpires) = tokens.CreateToken(user);
        return TypedResults.Ok(new AuthResponse(access, accessExpires, rotated.Token, rotated.ExpiresAt));
    }

    private static async Task<NoContent> Logout(
        RefreshTokenRequest req, RefreshTokenService refreshTokens, CancellationToken ct)
    {
        await refreshTokens.RevokeAsync(req.RefreshToken, ct);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> LogoutAll(
        ClaimsPrincipal user, RefreshTokenService refreshTokens, CancellationToken ct)
    {
        await refreshTokens.RevokeAllForUserAsync(user.GetRequiredUserId(), ct);
        return TypedResults.NoContent();
    }

    private static Ok<MeResponse> Me(ClaimsPrincipal user) =>
    TypedResults.Ok(new MeResponse(
        user.GetRequiredUserId(),
        user.FindFirst(JwtRegisteredClaimNames.Email)?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value));
}