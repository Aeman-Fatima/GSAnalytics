using GSAnalytics.Application.Auth;
using GSAnalytics.Application.Security;
using GSAnalytics.Domain.Entities;
using GSAnalytics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GSAnalytics.Infrastructure.Auth;

public class AuthService(GSAnalyticsDbContext db, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService) : IAuthService
{
    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail, ct))
        {
            throw new EmailAlreadyRegisteredException();
        }

        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = passwordHasher.Hash(request.Password),
            DisplayName = request.DisplayName.Trim(),
            CreatedAtUtc = now
        };

        var business = new Business
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            Name = request.BusinessName.Trim(),
            CreatedAtUtc = now
        };

        db.Users.Add(user);
        db.Businesses.Add(business);
        await db.SaveChangesAsync(ct);

        return await IssueTokensAsync(user, business, ct);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail, ct);
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        var business = await db.Businesses.SingleAsync(b => b.OwnerUserId == user.Id, ct);

        return await IssueTokensAsync(user, business, ct);
    }

    public async Task<AuthResult> RefreshAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        var hash = jwtTokenService.HashRefreshToken(rawRefreshToken);
        var existing = await db.RefreshTokens.SingleOrDefaultAsync(r => r.TokenHash == hash, ct);

        if (existing is null)
        {
            throw new InvalidRefreshTokenException();
        }

        if (existing.RevokedAtUtc is not null)
        {
            // The same refresh token was presented twice: it was already rotated away once, so this
            // is a strong signal of theft/replay. Revoke every other active token for this user.
            var others = await db.RefreshTokens
                .Where(r => r.UserId == existing.UserId && r.RevokedAtUtc == null)
                .ToListAsync(ct);
            foreach (var token in others)
            {
                token.RevokedAtUtc = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            throw new InvalidRefreshTokenException();
        }

        if (existing.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new InvalidRefreshTokenException();
        }

        var user = await db.Users.SingleAsync(u => u.Id == existing.UserId, ct);
        var business = await db.Businesses.SingleAsync(b => b.OwnerUserId == user.Id, ct);

        var result = await IssueTokensAsync(user, business, ct, replaces: existing);

        return result;
    }

    public async Task LogoutAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        var hash = jwtTokenService.HashRefreshToken(rawRefreshToken);
        var existing = await db.RefreshTokens.SingleOrDefaultAsync(r => r.TokenHash == hash, ct);
        if (existing is not null && existing.RevokedAtUtc is null)
        {
            existing.RevokedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<AuthResult> IssueTokensAsync(User user, Business business, CancellationToken ct, RefreshToken? replaces = null)
    {
        var accessToken = jwtTokenService.GenerateAccessToken(user.Id, business.Id, user.Email, user.DisplayName);
        var refreshToken = jwtTokenService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshToken.Hash,
            ExpiresAtUtc = refreshToken.ExpiresAtUtc,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.RefreshTokens.Add(refreshTokenEntity);

        if (replaces is not null)
        {
            replaces.RevokedAtUtc = DateTime.UtcNow;
            replaces.ReplacedByTokenId = refreshTokenEntity.Id;
        }

        await db.SaveChangesAsync(ct);

        return new AuthResult(
            accessToken.Value,
            accessToken.ExpiresAtUtc,
            refreshToken.Value,
            refreshToken.ExpiresAtUtc,
            user.Id,
            user.Email,
            user.DisplayName,
            business.Id,
            business.Name);
    }
}
