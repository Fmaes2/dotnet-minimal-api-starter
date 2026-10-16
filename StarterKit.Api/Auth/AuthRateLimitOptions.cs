namespace StarterKit.Api.Auth;

public class AuthRateLimitOptions
{
    public const string Section = "RateLimiting:Auth";
    public int PermitLimit { get; set; } = 5;
    public int WindowSeconds { get; set; } = 60;
}