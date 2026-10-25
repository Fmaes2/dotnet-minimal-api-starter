namespace StarterKit.Api.Auth;

public class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string Key { get; set; } = "";
    public int ExpiresMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 7;
    public int RefreshTokenReuseLeewaySeconds { get; set; } = 10;
}