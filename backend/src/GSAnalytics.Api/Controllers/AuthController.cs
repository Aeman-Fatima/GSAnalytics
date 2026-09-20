using GSAnalytics.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GSAnalytics.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("AuthPolicy")]
public class AuthController(IAuthService authService) : ControllerBase
{
    private const string RefreshCookieName = "refreshToken";

    public record RegisterBody(string Email, string Password, string DisplayName, string BusinessName);

    public record LoginBody(string Email, string Password);

    public record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, Guid UserId, string Email, string DisplayName, Guid BusinessId, string BusinessName);

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.DisplayName) || string.IsNullOrWhiteSpace(body.BusinessName))
        {
            return BadRequest(new { message = "Email, display name, and business name are required." });
        }

        if (body.Password is null || body.Password.Length < 8)
        {
            return BadRequest(new { message = "Password must be at least 8 characters." });
        }

        try
        {
            var result = await authService.RegisterAsync(new RegisterRequest(body.Email, body.Password, body.DisplayName, body.BusinessName), ct);
            return Ok(ToResponse(result));
        }
        catch (EmailAlreadyRegisteredException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginBody body, CancellationToken ct)
    {
        try
        {
            var result = await authService.LoginAsync(new LoginRequest(body.Email, body.Password), ct);
            return Ok(ToResponse(result));
        }
        catch (InvalidCredentialsException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        var rawRefreshToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(rawRefreshToken))
        {
            return Unauthorized(new { message = "No refresh token was presented." });
        }

        try
        {
            var result = await authService.RefreshAsync(rawRefreshToken, ct);
            return Ok(ToResponse(result));
        }
        catch (InvalidRefreshTokenException ex)
        {
            Response.Cookies.Delete(RefreshCookieName, CookiePath());
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var rawRefreshToken = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(rawRefreshToken))
        {
            await authService.LogoutAsync(rawRefreshToken, ct);
        }

        Response.Cookies.Delete(RefreshCookieName, CookiePath());
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        var businessId = User.FindFirst(Application.Security.JwtTokenService.BusinessIdClaimType)?.Value;
        var email = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value;
        var name = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Name)?.Value;

        return Ok(new { userId = sub, businessId, email, displayName = name });
    }

    private AuthResponse ToResponse(AuthResult result)
    {
        Response.Cookies.Append(RefreshCookieName, result.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            // The frontend and API are deliberately served from separate origins (see the CORS
            // comment in Program.cs) that also differ in scheme (http frontend, https API) —
            // browsers treat that as cross-site under "schemeful same-site" rules, so a Lax
            // cookie is silently dropped on cross-origin fetch(); SameSite=None is required for
            // it to survive. It's still HttpOnly+Secure+path-scoped, and only an origin on the
            // Cors:AllowedOrigins allowlist can even complete a credentialed request.
            SameSite = SameSiteMode.None,
            Expires = result.RefreshTokenExpiresAtUtc,
            Path = "/api/auth"
        });

        return new AuthResponse(
            result.AccessToken,
            result.AccessTokenExpiresAtUtc,
            result.UserId,
            result.Email,
            result.DisplayName,
            result.BusinessId,
            result.BusinessName);
    }

    private static CookieOptions CookiePath() => new() { Path = "/api/auth" };
}
