using System.Data.Common;
using Api.Models;

namespace Api.Managers.InterfacesDao;

/// <summary>
/// Interface for token data access operations.
/// </summary>
public interface ITokenDao
{
    /// <summary>
    /// Saves a token set by state asynchronously.
    /// </summary>
    /// <param name="state">The OAuth state value.</param>
    /// <param name="provider">The authentication provider name.</param>
    /// <param name="providerUserId">The provider user identifier.</param>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="scope">The granted scopes.</param>
    /// <param name="accessExpiresAt">The access token expiration date and time.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The ID of the saved token set.</returns>
    Task<long> SaveByStateAsync(string state, string provider, string providerUserId,
        string refreshToken, string scope, DateTime accessExpiresAt, CancellationToken ct = default);

    /// <summary>
    /// Attaches a token set to a session asynchronously.
    /// </summary>
    /// <param name="tokenSetId">The token set identifier.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    Task AttachToSessionAsync(long tokenSetId, string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Gets the token set associated with a session asynchronously.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The token set, or null if not found.</returns>
    Task<TokenSet?> GetBySessionAsync(string sessionId);

    /// <summary>
    /// Deletes the token set associated with a session asynchronously.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    Task DeleteBySessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Deletes the token set associated with a session within a transaction asynchronously.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="conn">The database connection.</param>
    /// <param name="tx">The database transaction.</param>
    Task DeleteBySessionAsync(string sessionId, DbConnection conn, DbTransaction tx);

    /// <summary>
    /// Asynchronously updates the token set after a refresh operation.
    /// </summary>
    /// <param name="sessionId">The identifier of the session associated with the token set.</param>
    /// <param name="refreshToken">The new refresh token to update.</param>
    /// <param name="newAccessExpiresAtUtc">The new UTC expiration date and time for the access token.</param>
    /// <param name="ct">Optional cancellation token for the asynchronous operation.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous update operation.</returns>
    Task UpdateAfterRefreshAsync(string sessionId, string refreshToken, DateTime newAccessExpiresAtUtc,
        CancellationToken ct = default);
}