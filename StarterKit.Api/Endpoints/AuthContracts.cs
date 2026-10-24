namespace StarterKit.Api.Endpoints;

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);
public record AuthResponse(
    string AccessToken, DateTime AccessTokenExpiresAt,
    string RefreshToken, DateTime RefreshTokenExpiresAt);
public record RefreshTokenRequest(string RefreshToken);
public record MeResponse(string Id, string? Email);