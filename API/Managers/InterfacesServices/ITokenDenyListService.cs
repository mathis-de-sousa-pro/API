namespace API.Managers.InterfacesServices;

/// <summary>
/// Provides methods to manage a denylist of refresh tokens.
/// </summary>
public interface ITokenDenyListService
{
    /// <summary>
    /// Checks if a refresh token hash is denied.
    /// </summary>
    /// <param name="refreshTokenHash">The hash of the refresh token.</param>
    /// <returns>True if denied; otherwise, false.</returns>
    Task<bool> IsDeniedAsync(string refreshTokenHash);

    /// <summary>
    /// Adds a refresh token hash to the denylist.
    /// </summary>
    /// <param name="refreshTokenHash">The hash of the refresh token.</param>
    /// <param name="reason">The reason for denial.</param>
    /// <param name="expiresAtUtc">The UTC expiration date and time for the deny entry (optional).</param>
    Task AddAsync(string refreshTokenHash, string reason, DateTime? expiresAtUtc);
}