using System.Net;
using System.Text;
using System.Text.Json;
using API.Helpers;
using API.Managers.InterfacesServices;
using Moq;
using Moq.Protected;

namespace Tests.Helpers;

public class SpotifyOAuthHelperTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new Mock<IHttpClientFactory>();
    private readonly Mock<IConfigService> _config = new Mock<IConfigService>();
    private readonly Mock<IClockService> _clock = new Mock<IClockService>();
    private readonly Mock<IAuditService> _audit = new Mock<IAuditService>();

    private HttpClient CreateMockClient(HttpResponseMessage response)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
        return new HttpClient(handler.Object);
    }

    [Fact]
    public async Task ExchangeCodeForTokensAsync_Throws_OnEmptyArgs()
    {
        var helper = new SpotifyOAuthHelper(_httpClientFactory.Object, _config.Object, _clock.Object, _audit.Object);
        await Assert.ThrowsAsync<ArgumentException>(() => helper.ExchangeCodeForTokensAsync("", "cb", "verifier"));
        await Assert.ThrowsAsync<ArgumentException>(() => helper.ExchangeCodeForTokensAsync("code", "", "verifier"));
        await Assert.ThrowsAsync<ArgumentException>(() => helper.ExchangeCodeForTokensAsync("code", "cb", ""));
    }

    [Fact]
    public async Task ExchangeCodeForTokensAsync_Throws_OnNetworkError()
    {
        _httpClientFactory.Setup(f => f.CreateClient("spotify-oauth")).Throws(new HttpRequestException("fail"));
        var helper = new SpotifyOAuthHelper(_httpClientFactory.Object, _config.Object, _clock.Object, _audit.Object);
        _config.Setup(c => c.GetSpotifyTokenEndpoint()).Returns("https://accounts.spotify.com/api/token");
        _config.Setup(c => c.GetSpotifyClientId()).Returns("clientId");
        await Assert.ThrowsAsync<HttpRequestException>(() => helper.ExchangeCodeForTokensAsync("code", "cb", "verifier"));
    }

    [Fact]
    public async Task RefreshTokensAsync_Throws_OnEmptyRefreshToken()
    {
        var helper = new SpotifyOAuthHelper(_httpClientFactory.Object, _config.Object, _clock.Object, _audit.Object);
        await Assert.ThrowsAsync<ArgumentException>(() => helper.RefreshTokensAsync(""));
    }

    [Fact]
    public async Task RefreshTokensAsync_Throws_OnNetworkError()
    {
        _httpClientFactory.Setup(f => f.CreateClient("spotify-oauth")).Throws(new HttpRequestException("fail"));
        var helper = new SpotifyOAuthHelper(_httpClientFactory.Object, _config.Object, _clock.Object, _audit.Object);
        _config.Setup(c => c.GetSpotifyTokenEndpoint()).Returns("https://accounts.spotify.com/api/token");
        _config.Setup(c => c.GetSpotifyClientId()).Returns("clientId");
        await Assert.ThrowsAsync<HttpRequestException>(() => helper.RefreshTokensAsync("refresh"));
    }

    [Fact]
    public async Task RefreshTokensAsync_ReturnsResult_OnValidResponse()
    {
        var now = DateTime.UtcNow;
        _clock.Setup(c => c.GetUtcNow()).Returns(now);
        _config.Setup(c => c.GetSpotifyTokenEndpoint()).Returns("https://accounts.spotify.com/api/token");
        _config.Setup(c => c.GetSpotifyClientId()).Returns("clientId");
        var responseObj = new
        {
            access_token = "access",
            refresh_token = "refresh2",
            expires_in = 3600
        };
        var json = JsonSerializer.Serialize(responseObj);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        var client = CreateMockClient(response);
        _httpClientFactory.Setup(f => f.CreateClient("spotify-oauth")).Returns(client);
        var helper = new SpotifyOAuthHelper(_httpClientFactory.Object, _config.Object, _clock.Object, _audit.Object);
        var result = await helper.RefreshTokensAsync("refresh");
        Assert.Equal("access", result.AccessToken);
        Assert.Equal("refresh2", result.NewRefreshToken);
        Assert.True(result.AccessExpiresAtUtc > now);
    }
}