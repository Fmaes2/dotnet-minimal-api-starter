using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace StarterKit.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static string GetRequiredUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new InvalidOperationException("The token has no 'sub' claim.");
}