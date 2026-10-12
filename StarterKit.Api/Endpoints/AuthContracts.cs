namespace StarterKit.Api.Endpoints;

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, DateTime ExpiresAt);
public record MeResponse(string Id, string? Email);