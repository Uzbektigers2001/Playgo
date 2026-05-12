using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Playgo.Application.DTOs.Auth;
using Playgo.Tests.Common;

namespace Playgo.Tests.Integration;

public class AuthEndpointsTests : IClassFixture<TestWebAppFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly TestWebAppFactory _factory;

    public AuthEndpointsTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_ReturnsCreatedWithTokens()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"u{Guid.NewGuid():N}@example.com",
            password = "password123",
            username = $"user_{Guid.NewGuid():N}".Substring(0, 12),
        });

        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
        body.User.Should().NotBeNull();
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        var client = _factory.CreateClient();
        var email = $"login{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "password123" });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { emailOrUsername = email, password = "password123" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await login.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        body!.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithBadCredentials_Returns400()
    {
        var client = _factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/auth/login", new { emailOrUsername = "nobody@example.com", password = "wrong" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Me_RequiresAuth()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/auth/me");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithBearerToken_ReturnsProfile()
    {
        var client = _factory.CreateClient();
        var email = $"me{Guid.NewGuid():N}@example.com";

        var reg = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "password123" });
        var auth = await reg.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var meResp = await client.GetAsync("/api/auth/me");
        meResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await meResp.Content.ReadFromJsonAsync<UserDto>(JsonOpts);
        user!.Email.Should().Be(email.ToLowerInvariant());
    }

    [Fact]
    public async Task Refresh_RotatesAccessToken()
    {
        var client = _factory.CreateClient();
        var email = $"refresh{Guid.NewGuid():N}@example.com";

        var reg = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "password123" });
        var auth = await reg.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            accessToken = auth!.AccessToken,
            refreshToken = auth.RefreshToken,
        });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);

        var rotated = await refresh.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        rotated!.AccessToken.Should().NotBeNullOrEmpty();
        rotated.RefreshToken.Should().NotBe(auth.RefreshToken);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var client = _factory.CreateClient();
        var email = $"logout{Guid.NewGuid():N}@example.com";

        var reg = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "password123" });
        var auth = await reg.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        var logout = await client.PostAsync("/api/auth/logout", content: null);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refreshAttempt = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            accessToken = auth.AccessToken,
            refreshToken = auth.RefreshToken,
        });
        refreshAttempt.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
