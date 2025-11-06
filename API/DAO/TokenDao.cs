using System.Data;
using System.Data.Common;
using Api.Managers.InterfacesDao;
using API.Managers.InterfacesServices;
using Api.Models;

namespace API.DAO;

/// <summary>
/// Data Access Object for managing TokenSet entries in the database.
/// Provides methods to save, attach, retrieve, and delete token sets by state or session.
/// </summary>
public class TokenDao(ISqlConnectionFactory factory, IClockService clock) : ITokenDao
{
    private readonly ISqlConnectionFactory _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    private readonly IClockService _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <inheritdoc />
    public async Task<long> SaveByStateAsync(string state, string provider, string providerUserId,
        string refreshTokenEnc, string scope, DateTime accessExpiresAt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("provider cannot be null or empty.", nameof(provider));
        if (string.IsNullOrWhiteSpace(refreshTokenEnc))
            throw new ArgumentException("refreshTokenEnc cannot be null or empty.", nameof(refreshTokenEnc));

        DateTime now = _clock.GetUtcNow();

        const string sql = @"
                            INSERT INTO tokenset (Provider, ProviderUserId, RefreshTokenEnc, Scope, AccessExpiresAt, UpdatedAt)
                            VALUES (@provider, @puid, @refresh, @scope, @accessExp, @updatedAt);
                            SELECT LAST_INSERT_ID();";

        await using DbConnection conn = await _factory.CreateOpenAsync(ct);
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        DbParameter paramProvider = cmd.CreateParameter();
        paramProvider.ParameterName = "@provider";
        paramProvider.Value = provider;
        cmd.Parameters.Add(paramProvider);
        DbParameter paramPuid = cmd.CreateParameter();
        paramPuid.ParameterName = "@puid";
        paramPuid.Value = providerUserId;
        cmd.Parameters.Add(paramPuid);
        DbParameter paramRefresh = cmd.CreateParameter();
        paramRefresh.ParameterName = "@refresh";
        paramRefresh.Value = refreshTokenEnc;
        cmd.Parameters.Add(paramRefresh);
        DbParameter paramScope = cmd.CreateParameter();
        paramScope.ParameterName = "@scope";
        paramScope.Value = scope;
        cmd.Parameters.Add(paramScope);
        DbParameter paramAccessExp = cmd.CreateParameter();
        paramAccessExp.ParameterName = "@accessExp";
        paramAccessExp.Value = accessExpiresAt;
        cmd.Parameters.Add(paramAccessExp);
        DbParameter paramUpdatedAt = cmd.CreateParameter();
        paramUpdatedAt.ParameterName = "@updatedAt";
        paramUpdatedAt.Value = now;
        cmd.Parameters.Add(paramUpdatedAt);

        object scalar = await cmd.ExecuteScalarAsync(ct) ?? throw new DataException("Failed to execute insert for TOKENSET.");
        if (scalar == null || scalar == DBNull.Value)
            throw new DataException("Failed to retrieve LAST_INSERT_ID for TOKENSET.");

        bool ok = long.TryParse(Convert.ToString(scalar), out long id);
        if (!ok || id <= 0) throw new DataException("Invalid LAST_INSERT_ID for TOKENSET.");
        return id;
    }

    /// <inheritdoc />
    public async Task AttachToSessionAsync(long tokenSetId, string sessionId, CancellationToken ct = default)
    {
        if (tokenSetId <= 0)
            throw new ArgumentException("tokenSetId must be positive.", nameof(tokenSetId));

        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("sessionId cannot be null or empty.", nameof(sessionId));

        DateTime now = _clock.GetUtcNow();

        const string sql = @"
                            UPDATE tokenset
                            SET SessionId = @sid, UpdatedAt = @updatedAt
                            WHERE TokenSetId = @id";

        await using DbConnection conn = await _factory.CreateOpenAsync(ct);
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        DbParameter paramSid = cmd.CreateParameter();
        paramSid.ParameterName = "@sid";
        paramSid.Value = sessionId;
        cmd.Parameters.Add(paramSid);
        DbParameter paramUpdatedAt = cmd.CreateParameter();
        paramUpdatedAt.ParameterName = "@updatedAt";
        paramUpdatedAt.Value = now;
        cmd.Parameters.Add(paramUpdatedAt);
        DbParameter paramId = cmd.CreateParameter();
        paramId.ParameterName = "@id";
        paramId.Value = tokenSetId;
        cmd.Parameters.Add(paramId);

        int affected = await cmd.ExecuteNonQueryAsync(ct);
        if (affected != 1)
            throw new DataException("Unexpected number of rows updated for TOKENSET.AttachToSession.");
    }

    /// <inheritdoc />
    public async Task<TokenSet?> GetBySessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("sessionId cannot be null or empty.", nameof(sessionId));

        const string sql = @"
                            SELECT TokenSetId, Provider, ProviderUserId, RefreshTokenEnc, Scope, AccessExpiresAt, UpdatedAt, SessionId
                            FROM tokenset
                            WHERE SessionId = @sid
                            LIMIT 1";

        TokenSet? result = null;

        await using DbConnection conn = await _factory.CreateOpenAsync();
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        DbParameter paramSid = cmd.CreateParameter();
        paramSid.ParameterName = "@sid";
        paramSid.Value = sessionId;
        cmd.Parameters.Add(paramSid);

        await using DbDataReader reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            result = new TokenSet(
                tokenSetId: reader.GetInt64("TokenSetId"),
                provider: reader.GetString("Provider"),
                providerUserId: reader.GetString("ProviderUserId"),
                refreshToken: reader.GetString("RefreshTokenEnc"),
                scope: reader.IsDBNull(reader.GetOrdinal("Scope")) ? string.Empty : reader.GetString("Scope"),
                accessExpiresAt: reader.GetDateTime("AccessExpiresAt"),
                updatedAt: reader.GetDateTime("UpdatedAt"),
                sessionId: (reader.IsDBNull(reader.GetOrdinal("SessionId")) ? null : reader.GetString("SessionId")) ??
                           throw new InvalidOperationException("SessionId should not be null here.")
            );
        }

        return result;
    }

    /// <inheritdoc />
    public async Task DeleteBySessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("sessionId cannot be null or empty.", nameof(sessionId));

        const string sql = "delete from tokenset where SessionId = @sid";

        await using DbConnection conn = await _factory.CreateOpenAsync(ct);
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        DbParameter paramSid = cmd.CreateParameter();
        paramSid.ParameterName = "@sid";
        paramSid.Value = sessionId;
        cmd.Parameters.Add(paramSid);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteBySessionAsync(string sessionId, DbConnection conn, DbTransaction tx)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("sessionId cannot be null or empty.", nameof(sessionId));
        if (conn is null || tx is null)
            throw new ArgumentNullException(nameof(conn));

        const string sql = "delete from tokenset where SessionId = @sid";

        await using DbCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        DbParameter paramSid = cmd.CreateParameter();
        paramSid.ParameterName = "@sid";
        paramSid.Value = sessionId;
        cmd.Parameters.Add(paramSid);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Asynchronously updates the token set after a refresh operation for the given session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="refreshToken">The new encrypted refresh token.</param>
    /// <param name="newAccessExpiresAtUtc">The new access token expiration date in UTC.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task representing the asynchronous update operation.</returns>
    public async Task UpdateAfterRefreshAsync(string sessionId, string refreshToken, DateTime newAccessExpiresAtUtc,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("sessionId cannot be null or empty.", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("refreshToken cannot be null or empty.", nameof(refreshToken));

        DateTime now = _clock.GetUtcNow();

        const string sql = @"
                            update tokenset
                            set RefreshTokenEnc = @refresh,
                                AccessExpiresAt = @accessExp,
                                UpdatedAt       = @updatedAt
                            where SessionId = @sid
                            limit 1;";

        await using DbConnection conn = await _factory.CreateOpenAsync(ct);
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        DbParameter paramRefresh = cmd.CreateParameter();
        paramRefresh.ParameterName = "@refresh";
        paramRefresh.Value = refreshToken;
        cmd.Parameters.Add(paramRefresh);
        DbParameter paramAccessExp = cmd.CreateParameter();
        paramAccessExp.ParameterName = "@accessExp";
        paramAccessExp.Value = newAccessExpiresAtUtc;
        cmd.Parameters.Add(paramAccessExp);
        DbParameter paramUpdatedAt = cmd.CreateParameter();
        paramUpdatedAt.ParameterName = "@updatedAt";
        paramUpdatedAt.Value = now;
        cmd.Parameters.Add(paramUpdatedAt);
        DbParameter paramSid = cmd.CreateParameter();
        paramSid.ParameterName = "@sid";
        paramSid.Value = sessionId;
        cmd.Parameters.Add(paramSid);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}