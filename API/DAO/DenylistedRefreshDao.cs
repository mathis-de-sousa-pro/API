using System.Data.Common;
using Api.Managers.InterfacesDao;
using API.Managers.InterfacesServices;

namespace API.DAO;

/// <summary>
/// MySQL implementation for the DENYLISTEDREFRESH table.
/// Provides methods to check existence and upsert denylisted refresh tokens.
/// </summary>
public class DenylistedRefreshDao : IDenylistedRefreshDao
{
    private readonly ISqlConnectionFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DenylistedRefreshDao"/> class.
    /// </summary>
    /// <param name="factory">The SQL connection factory used to create database connections.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="factory"/> is null.</exception>
    public DenylistedRefreshDao(ISqlConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string refreshHash, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(refreshHash))
            throw new ArgumentException("refreshHash cannot be null or empty.", nameof(refreshHash));

        const string sql = @"
            SELECT 1
            FROM denylistedrefresh
            WHERE RefreshHash = @h AND ExpiresAt > @now
            LIMIT 1;";

        var ct = CancellationToken.None;
        await using DbConnection conn = await _factory.CreateOpenAsync(ct);
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var paramH = cmd.CreateParameter();
        paramH.ParameterName = "@h";
        paramH.Value = refreshHash;
        cmd.Parameters.Add(paramH);

        var paramNow = cmd.CreateParameter();
        paramNow.ParameterName = "@now";
        paramNow.Value = nowUtc;
        cmd.Parameters.Add(paramNow);

        var obj = await cmd.ExecuteScalarAsync(ct);
        return obj != null && obj != DBNull.Value;
    }

    /// <inheritdoc />
    public async Task UpsertAsync(string refreshHash, string reason, DateTime addedAtUtc, DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(refreshHash))
            throw new ArgumentException("refreshHash cannot be null or empty.", nameof(refreshHash));

        const string sql = @"
            INSERT INTO denylistedrefresh(RefreshHash, Reason, AddedAt, ExpiresAt)
            VALUES (@h, @r, @added, @exp)
            ON DUPLICATE KEY UPDATE
                Reason = VALUES(Reason),
                AddedAt = VALUES(AddedAt),
                ExpiresAt = VALUES(ExpiresAt);";

        var ct = CancellationToken.None;
        await using DbConnection conn = await _factory.CreateOpenAsync(ct);
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var paramH = cmd.CreateParameter();
        paramH.ParameterName = "@h";
        paramH.Value = refreshHash;
        cmd.Parameters.Add(paramH);

        var paramR = cmd.CreateParameter();
        paramR.ParameterName = "@r";
        paramR.Value = reason ?? "logout";
        cmd.Parameters.Add(paramR);

        var paramAdded = cmd.CreateParameter();
        paramAdded.ParameterName = "@added";
        paramAdded.Value = addedAtUtc;
        cmd.Parameters.Add(paramAdded);

        var paramExp = cmd.CreateParameter();
        paramExp.ParameterName = "@exp";
        paramExp.Value = expiresAtUtc;
        cmd.Parameters.Add(paramExp);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}