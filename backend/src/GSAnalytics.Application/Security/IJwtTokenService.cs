namespace GSAnalytics.Application.Security;

public record AccessToken(string Value, DateTime ExpiresAtUtc);

public record RawRefreshToken(string Value, string Hash, DateTime ExpiresAtUtc);

public interface IJwtTokenService
{
    AccessToken GenerateAccessToken(Guid userId, Guid businessId, string email, string displayName);

    RawRefreshToken GenerateRefreshToken();

    /// <summary>SHA-256 hex digest of a raw refresh token, used to look up / verify stored tokens.</summary>
    string HashRefreshToken(string rawToken);
}
