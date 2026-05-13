using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Playgo.Application.DTOs.Auth;
using Playgo.Tests.Common;

namespace Playgo.Tests.Integration;

public class AdminEndpointsTests : IClassFixture<TestWebAppFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly TestWebAppFactory _factory;

    public AdminEndpointsTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task AdminEndpoints_RequireAuth()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/admin/dashboard/stats");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoints_RegularUserGets403()
    {
        var client = _factory.CreateClient();
        var email = $"regular{Guid.NewGuid():N}@example.com";

        var reg = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "password123" });
        reg.EnsureSuccessStatusCode();
        var auth = await reg.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        var resp = await client.GetAsync("/api/admin/dashboard/stats");
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminEndpoints_SeededAdminCanAccess()
    {
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            emailOrUsername = "admin@playgo.uz",
            password = "Admin123!",
        });
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var resp = await client.GetAsync("/api/admin/dashboard/stats");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
