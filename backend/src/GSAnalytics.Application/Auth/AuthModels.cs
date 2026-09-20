namespace GSAnalytics.Application.Auth;

public record RegisterRequest(string Email, string Password, string DisplayName, string BusinessName);

public record LoginRequest(string Email, string Password);

public record AuthResult(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    Guid UserId,
    string Email,
    string DisplayName,
    Guid BusinessId,
    string BusinessName);

public abstract class AuthException(string message) : Exception(message);

public class EmailAlreadyRegisteredException() : AuthException("An account with this email already exists.");

public class InvalidCredentialsException() : AuthException("Incorrect email or password.");

public class InvalidRefreshTokenException() : AuthException("The refresh token is missing, expired, or has already been used.");
