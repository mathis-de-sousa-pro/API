using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.DTO;
using API.Errors;
using Api.Managers.InterfacesHelpers;
using API.Managers.InterfacesServices;

namespace API.Helpers;

/// <summary>
/// Helper class for managing Spotify OAuth token exchange and user profile retrieval.
/// </summary>
public class SpotifyOAuthHelper(
    IHttpClientFactory httpClientFactory,
    IConfigService config,
    IClockService clock,
    IAuditService audit)
    : ISpotifyOAuthHelper
{
    #region Fields

    /// <summary>
    /// Factory for creating HTTP clients.
    /// </summary>
    private readonly IHttpClientFactory _httpClientFactory =
        httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

    /// <summary>
    /// Service for retrieving configuration values.
    /// </summary>
    private readonly IConfigService _config = config ?? throw new ArgumentNullException(nameof(config));

    /// <summary>
    /// Service for retrieving the current UTC time.
    /// </summary>
    private readonly IClockService _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <summary>
    /// Service for logging audit events.
    /// </summary>
    private readonly IAuditService _audit = audit ?? throw new ArgumentNullException(nameof(audit));

    #endregion

    #region Public Methods

    /// <summary>
    /// Exchanges an authorization code for access and refresh tokens.
    /// </summary>
    /// <param name="code">The authorization code received from Spotify.</param>
    /// <param name="redirectUri">The redirect URI used during the authorization request.</param>
    /// <param name="codeVerifier">The PKCE code verifier used during the authorization request.</param>
    /// <returns>A <see cref="TokenInfo"/> object containing the tokens and user information.</returns>
    /// <exception cref="ArgumentException">Thrown if any input parameter is null or empty.</exception>
    /// <exception cref="TokenExchangeFailedException">Thrown if the token exchange fails.</exception>
    public async Task<TokenInfo> ExchangeCodeForTokensAsync(string code, string redirectUri, string codeVerifier)
    {
        ValidateInput(code, redirectUri, codeVerifier);

        string tokenEndpoint = _config.GetSpotifyTokenEndpoint();
        string clientId = _config.GetSpotifyClientId();

        string body = BuildTokenRequestBody(code, redirectUri, clientId, codeVerifier);
        JsonDocument tokenResponse = await SendTokenRequestAsync(tokenEndpoint, body);

        string accessToken = ReadString(tokenResponse, "access_token");
        string refreshToken = ReadString(tokenResponse, "refresh_token");
        string scope = ReadString(tokenResponse, "scope");
        int expiresIn = ReadInt(tokenResponse, "expires_in", 3600);

        ValidateTokenResponse(accessToken, refreshToken);

        DateTime accessExpiresAt = CalculateAccessExpiry(expiresIn);

        string providerUserId = await FetchSpotifyUserIdAsync(accessToken);

        await _audit.LogAuthAsync("spotify", "auth_success", new { userId = providerUserId }, userId: providerUserId);
        return new TokenInfo(accessToken, refreshToken, accessExpiresAt, scope, providerUserId);
    }

    /// <summary>
    /// Refreshes the access token using a refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token provided by Spotify.</param>
    /// <param name="ct">A cancellation token for the operation.</param>
    /// <returns>A <see cref="RefreshResult"/> object containing the new access token and optional new refresh token.</returns>
    /// <exception cref="ArgumentException">Thrown if the refresh token is null or empty.</exception>
    /// <exception cref="TokenExchangeFailedException">Thrown if the token refresh fails.</exception>
    public async Task<RefreshResult> RefreshTokensAsync(string refreshToken, CancellationToken ct = default)
    {
        ValidateInput(refreshToken, nameof(refreshToken));

        string tokenEndpoint = _config.GetSpotifyTokenEndpoint();
        string clientId = _config.GetSpotifyClientId();

        string body = BuildRefreshTokenRequestBody(refreshToken, clientId);
        JsonDocument doc = await SendTokenRequestAsync(tokenEndpoint, body);

        string newAccessToken = ReadString(doc, "access_token");
        string maybeNewRefreshToken = ReadString(doc, "refresh_token");
        int expiresIn = ReadInt(doc, "expires_in", 3600);

        if (string.IsNullOrWhiteSpace(newAccessToken))
            throw new TokenExchangeFailedException("Refresh response missing access_token.");

        DateTime accessExpiresAt = CalculateAccessExpiry(expiresIn);

        return new RefreshResult(
            newAccessToken,
            accessExpiresAt,
            string.IsNullOrWhiteSpace(maybeNewRefreshToken) ? null : maybeNewRefreshToken
        );
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Validates that a string input is not null or empty.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="paramName">The name of the parameter being validated.</param>
    /// <exception cref="ArgumentException">Thrown if the value is null or empty.</exception>
    private void ValidateInput(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} cannot be null or empty.", paramName);
    }

    /// <summary>
    /// Validates the inputs for the token exchange process.
    /// </summary>
    private void ValidateInput(string code, string redirectUri, string codeVerifier)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("code cannot be null or empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(redirectUri))
            throw new ArgumentException("redirectUri cannot be null or empty.", nameof(redirectUri));
        if (string.IsNullOrWhiteSpace(codeVerifier))
            throw new ArgumentException("codeVerifier cannot be null or empty.", nameof(codeVerifier));
    }

    /// <summary>
    /// Builds the request body for exchanging an authorization code for tokens.
    /// </summary>
    private string BuildTokenRequestBody(string code, string redirectUri, string clientId, string codeVerifier)
    {
        return "grant_type=authorization_code"
               + "&code=" + Uri.EscapeDataString(code)
               + "&redirect_uri=" + Uri.EscapeDataString(redirectUri)
               + "&client_id=" + Uri.EscapeDataString(clientId)
               + "&code_verifier=" + Uri.EscapeDataString(codeVerifier);
    }

    /// <summary>
    /// Builds the request body for refreshing tokens.
    /// </summary>
    private string BuildRefreshTokenRequestBody(string refreshToken, string clientId)
    {
        return "grant_type=refresh_token"
               + "&refresh_token=" + Uri.EscapeDataString(refreshToken)
               + "&client_id=" + Uri.EscapeDataString(clientId);
    }

    /// <summary>
    /// Sends a token request to the Spotify token endpoint.
    /// </summary>
    private async Task<JsonDocument> SendTokenRequestAsync(string tokenEndpoint, string body)
    {
        HttpClient http = _httpClientFactory.CreateClient("spotify-oauth");
        HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded")
        };

        HttpResponseMessage resp;
        try
        {
            resp = await http.SendAsync(req);
        }
        catch (Exception ex)
        {
            await _audit.LogAuthAsync("spotify", "token_exchange.network_error", ex.Message).ConfigureAwait(false);
            throw new TokenExchangeFailedException("Network error during token exchange.", ex);
        }

        string payload = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            await HandleTokenRequestErrorAsync(resp, payload).ConfigureAwait(false);

        try
        {
            return JsonDocument.Parse(payload);
        }
        catch (Exception ex)
        {
            await _audit.LogAuthAsync("spotify", "token_exchange.parse_error", ex.Message).ConfigureAwait(false);
            throw new TokenExchangeFailedException("Failed to parse token response.", ex);
        }
    }

    /// <summary>
    /// Handles errors returned by the Spotify token endpoint.
    /// </summary>
    private async Task HandleTokenRequestErrorAsync(HttpResponseMessage resp, string payload)
    {
        string detail = "HTTP " + ((int)resp.StatusCode).ToString() + " payload: " + payload;
        await _audit.LogAuthAsync("spotify", "token_exchange.http_error", detail).ConfigureAwait(false);

        if ((int)resp.StatusCode == 400)
            throw new TokenExchangeFailedException("Invalid authorization code or PKCE verifier.");
        if ((int)resp.StatusCode == 429)
            throw new TokenExchangeFailedException("Rate limited by Spotify during token exchange.");

        throw new TokenExchangeFailedException("Spotify token endpoint returned an error.");
    }

    /// <summary>
    /// Validates the token response for required fields.
    /// </summary>
    private void ValidateTokenResponse(string accessToken, string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new TokenExchangeFailedException("Token response missing access_token.");
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new TokenExchangeFailedException("Token response missing refresh_token.");
    }

    /// <summary>
    /// Calculates the expiration time for an access token.
    /// </summary>
    private DateTime CalculateAccessExpiry(int expiresIn)
    {
        DateTime now = _clock.GetUtcNow();
        return now.AddSeconds(Math.Max(expiresIn - 60, 0));
    }

    /// <summary>
    /// Fetches the Spotify user ID using the access token.
    /// </summary>
    private async Task<string> FetchSpotifyUserIdAsync(string accessToken)
    {
        string meEndpoint = "https://api.spotify.com/v1/me";
        HttpClient http = _httpClientFactory.CreateClient("spotify-oauth");
        HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, meEndpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage resp;
        try
        {
            resp = await http.SendAsync(req);
        }
        catch (Exception ex)
        {
            await _audit.LogAuthAsync("spotify", "me.network_error", ex.Message).ConfigureAwait(false);
            throw new TokenExchangeFailedException("Network error fetching Spotify profile.", ex);
        }

        string payload = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            string detail = "HTTP " + ((int)resp.StatusCode).ToString() + " payload: " + payload;
            await _audit.LogAuthAsync("spotify", "me.http_error", detail).ConfigureAwait(false);
            throw new TokenExchangeFailedException("Failed to fetch Spotify user profile.");
        }

        try
        {
            JsonDocument doc = JsonDocument.Parse(payload);
            string providerUserId = ReadString(doc, "id");
            if (string.IsNullOrWhiteSpace(providerUserId))
                throw new TokenExchangeFailedException("Spotify profile missing id.");
            return providerUserId;
        }
        catch (Exception ex)
        {
            await _audit.LogAuthAsync("spotify", "me.parse_error", ex.Message).ConfigureAwait(false);
            throw new TokenExchangeFailedException("Failed to parse Spotify user profile.", ex);
        }
    }

    /// <summary>
    /// Reads a string property from a JSON document.
    /// </summary>
    private string ReadString(JsonDocument doc, string property)
    {
        if (doc == null) return string.Empty;
        if (!doc.RootElement.TryGetProperty(property, out JsonElement el)) return string.Empty;
        if (el.ValueKind == JsonValueKind.String) return el.GetString() ?? string.Empty;
        return el.ToString();
    }

    /// <summary>
    /// Reads an integer property from a JSON document, with a default value.
    /// </summary>
    private int ReadInt(JsonDocument doc, string property, int defaultValue)
    {
        if (doc == null) return defaultValue;
        if (!doc.RootElement.TryGetProperty(property, out JsonElement el)) return defaultValue;

        if (el.ValueKind == JsonValueKind.Number)
        {
            bool ok = el.TryGetInt32(out int value);
            if (ok) return value;
        }

        return defaultValue;
    }

    #endregion
}