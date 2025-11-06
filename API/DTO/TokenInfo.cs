namespace API.DTO;

/// <summary>
/// Data Transfer Object (DTO) containing information about OAuth tokens and related metadata.
/// </summary>
public class TokenInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TokenInfo"/> class.
    /// </summary>
    /// <param name="accessToken">The access token string used for authentication.</param>
    /// <param name="refreshToken">The refresh token string used to obtain new access tokens.</param>
    /// <param name="accessExpiresAt">The UTC expiration date and time of the access token.</param>
    /// <param name="scope">The OAuth scopes granted to the token.</param>
    /// <param name="providerUserId">The user ID at the OAuth provider.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="accessToken"/> or <paramref name="refreshToken"/> is null or empty.
    /// </exception>
    public TokenInfo(string accessToken, string refreshToken, DateTime accessExpiresAt, string scope, string providerUserId)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        AccessExpiresAt = accessExpiresAt;
        Scope = scope;
        ProviderUserId = providerUserId;
    }

    /// <summary>
    /// Gets or sets the access token string used for authentication.
    /// </summary>
    public string AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the refresh token string used to obtain new access tokens.
    /// </summary>
    public string RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the UTC expiration date and time of the access token.
    /// </summary>
    public DateTime AccessExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the OAuth scopes granted to the token.
    /// </summary>
    public string Scope { get; set; }

    /// <summary>
    /// Gets or sets the user ID at the OAuth provider.
    /// </summary>
    public string ProviderUserId { get; set; }
}