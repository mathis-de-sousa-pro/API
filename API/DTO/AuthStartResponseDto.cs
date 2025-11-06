namespace API.DTO;

/// <summary>
/// DTO representing the response for the start of authentication, including the authorization URL and state.
/// </summary>
public class AuthStartResponseDto
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthStartResponseDto"/> class.
    /// </summary>
    /// <param name="authorizationUrl">The authorization URL to which the user should be redirected.</param>
    /// <param name="state">The state parameter for PKCE validation.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="authorizationUrl"/> or <paramref name="state"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="authorizationUrl"/> or <paramref name="state"/> is empty or whitespace.
    /// </exception>
    public AuthStartResponseDto(string authorizationUrl, string state)
    {
        AuthorizationUrl = authorizationUrl;
        State = state;
    }

    /// <summary>
    /// Gets or sets the authorization URL to which the user should be redirected.
    /// </summary>
    /// <value>
    /// A non-null, non-empty string representing the authorization URL.
    /// </value>
    public string AuthorizationUrl { get; set; }

    /// <summary>
    /// Gets or sets the state parameter for PKCE validation.
    /// </summary>
    /// <value>
    /// A non-null, non-empty string representing the state.
    /// </value>
    public string State { get; set; }
}