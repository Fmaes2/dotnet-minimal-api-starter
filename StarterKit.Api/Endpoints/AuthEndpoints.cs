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
        LoginRequest req, UserManager<IdentityUser> users, TokenService tokens)
    {
        var user = await users.FindByEmailAsync(req.Email);
        if (user is null || !await users.CheckPasswordAsync(user, req.Password))
            return TypedResults.Unauthorized();

        var (token, expires) = tokens.CreateToken(user);
        return TypedResults.Ok(new AuthResponse(token, expires));
    }

    private static Ok<MeResponse> Me(ClaimsPrincipal user) =>
    TypedResults.Ok(new MeResponse(
        user.GetRequiredUserId(),
        user.FindFirst(JwtRegisteredClaimNames.Email)?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value));
}