namespace GSAnalytics.Application.Security;

/// <summary>
/// Bound from the "Jwt" configuration section. Key is a secret and must only ever come from
/// User Secrets locally / a secret store in real deployments — never from a committed appsettings file.
/// </summary>
public class JwtSettings
{
    public required string Key { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;
}
