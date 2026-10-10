using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace TreTrip.Api.Tests;

public class ApiAuthorizationTests
    : IClassFixture<AuthorizationApiFactory>
{
    private readonly AuthorizationApiFactory _factory;
    private readonly HttpClient _client;

    public ApiAuthorizationTests(
        AuthorizationApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/admin/test")]
    [InlineData("/api/trips")]
    [InlineData("/api/driver/trips")]
    public async Task MissingToken_Returns401(string path)
    {
        using var response = await _client.GetAsync(path);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Theory]
    [InlineData("Scheduler", "/api/admin/test")]
    [InlineData("Driver", "/api/admin/test")]
    [InlineData("Admin", "/api/trips")]
    [InlineData("Scheduler", "/api/driver/trips")]
    [InlineData("Driver", "/api/trips")]
    public async Task WrongRole_Returns403(
        string role, string path)
    {
        using var request =
            new HttpRequestMessage(HttpMethod.Get, path);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", _factory.CreateToken(role));

        using var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanAccessAdminEndpoint()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/api/admin/test");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", _factory.CreateToken("Admin"));

        using var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Fact]
    public async Task InvalidToken_Returns401()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/api/admin/test");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer", "invalid-jwt-token");

        using var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }
}