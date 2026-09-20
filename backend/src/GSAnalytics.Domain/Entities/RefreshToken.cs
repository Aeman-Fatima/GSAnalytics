namespace GSAnalytics.Domain.Entities;

/// <summary>
/// A single refresh token issuance. Only the SHA-256 hash of the raw token is stored, so a
/// database read alone never yields a usable credential. Rotation-on-use plus reuse detection
/// (see AuthService.RefreshAsync) limits the damage if a refresh cookie is ever stolen.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid? ReplacedByTokenId { get; set; }

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}
