using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GSAnalytics.Tests;

public class AuthControllerTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private record RegisterBody(string Email, string Password, string DisplayName, string BusinessName);
    private record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc, Guid UserId, string Email, string DisplayName, Guid BusinessId, string BusinessName);

    private static RegisterBody NewRegisterBody() => new(
        $"test-{Guid.NewGuid():N}@example.com", "TestPass123!", "Test User", "Test Business");

    [Fact]
    public async Task Register_ThenMe_ReturnsSameIdentity()
    {
        var client = factory.CreateClient();
        var body = NewRegisterBody();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", body);
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        var meResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var meBody = await meResponse.Content.ReadAsStringAsync();
        Assert.Contains(auth.UserId.ToString(), meBody);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        var client = factory.CreateClient();
        var body = NewRegisterBody();

        await client.PostAsJsonAsync("/api/auth/register", body);
        var secondResponse = await client.PostAsJsonAsync("/api/auth/register", body);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();
        var body = NewRegisterBody();
        await client.PostAsJsonAsync("/api/auth/register", body);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { body.Email, Password = "WrongPassword1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesCookie_AndOldCookieIsRejectedAfterReuse()
    {
        // Driven via raw Set-Cookie/Cookie headers so each token in the rotation chain stays inspectable.
        var client = factory.CreateClient();
        var body = NewRegisterBody();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", body);
        var setCookie = registerResponse.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("refreshToken="));
        var originalCookie = setCookie.Split(';')[0];

        var firstRefreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        firstRefreshRequest.Headers.Add("Cookie", originalCookie);
        var firstRefreshResponse = await client.SendAsync(firstRefreshRequest);
        Assert.Equal(HttpStatusCode.OK, firstRefreshResponse.StatusCode);

        // Reusing the original (now-rotated-away) cookie must fail...
        var reuseRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        reuseRequest.Headers.Add("Cookie", originalCookie);
        var reuseResponse = await client.SendAsync(reuseRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);

        // ...and because reuse was detected, the token it was rotated INTO must be revoked too.
        var newSetCookie = firstRefreshResponse.Headers.GetValues("Set-Cookie").First(c => c.StartsWith("refreshToken="));
        var rotatedCookie = newSetCookie.Split(';')[0];
        var secondRefreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        secondRefreshRequest.Headers.Add("Cookie", rotatedCookie);
        var secondRefreshResponse = await client.SendAsync(secondRefreshRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, secondRefreshResponse.StatusCode);
    }
}
