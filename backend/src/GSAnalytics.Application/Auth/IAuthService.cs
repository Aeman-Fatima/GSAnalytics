namespace GSAnalytics.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>Rotates the refresh token: the presented one is revoked and a new pair is issued.</summary>
    Task<AuthResult> RefreshAsync(string rawRefreshToken, CancellationToken ct = default);

    Task LogoutAsync(string rawRefreshToken, CancellationToken ct = default);
}
