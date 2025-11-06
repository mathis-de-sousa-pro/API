namespace API.DTO;

/// <summary>
/// DTO for starting an authentication request with specified scopes.
/// </summary>
public class AuthStartRequestDto
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthStartRequestDto"/> class.
    /// </summary>
    /// <param name="scopes">The list of scopes requested for authentication.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="scopes"/> is null or empty.</exception>
    public AuthStartRequestDto(IList<string> scopes)
    {
        Scopes = scopes;
    }

    /// <summary>
    /// Gets or sets the list of scopes requested for authentication.
    /// </summary>
    public IList<string> Scopes { get; set; }
}