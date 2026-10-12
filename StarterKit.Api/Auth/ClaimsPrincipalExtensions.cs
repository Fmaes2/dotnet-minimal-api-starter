using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace StarterKit.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static string GetRequiredUserId(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? throw new InvalidOperationException("The authenticated user has no id claim.");
}