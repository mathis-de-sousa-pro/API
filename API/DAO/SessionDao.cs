using System.Data.Common;
using Api.Managers.InterfacesDao;
using API.Managers.InterfacesServices;
using Api.Models;
// ISqlConnectionFactory

namespace API.DAO;

/// <summary>
/// Data Access Object for managing application sessions in the database.
/// Provides methods to insert, retrieve, and delete session entries.
/// </summary>
public class SessionDao(ISqlConnectionFactory factory) : ISessionDao
{
    private readonly ISqlConnectionFactory _factory = factory ?? throw new ArgumentNullException(nameof(factory));

    /// <inheritdoc />
    public async Task InsertAsync(AppSession session, CancellationToken ct = default)
    {
        if (session is null)
            throw new ArgumentNullException(nameof(session));

        const string sql = @"
INSERT INTO appsession (SessionId, DeviceInfo, CreatedAt, LastSeenAt, ExpiresAt)
VALUES (@sid, @device, @created, @lastseen, @expires)";

        await using DbConnection conn = await _factory.CreateOpenAsync(ct);
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var pSid = cmd.CreateParameter();
        pSid.ParameterName = "@sid";
        pSid.Value = session.SessionId;
        cmd.Parameters.Add(pSid);
        var pDev = cmd.CreateParameter();
        pDev.ParameterName = "@device";
        pDev.Value = session.DeviceInfo;
        cmd.Parameters.Add(pDev);
        var pCrt = cmd.CreateParameter();
        pCrt.ParameterName = "@created";
        pCrt.Value = session.CreatedAt;
        cmd.Parameters.Add(pCrt);
        var pLst = cmd.CreateParameter();
        pLst.ParameterName = "@lastseen";
        pLst.Value = session.LastSeenAt;
        cmd.Parameters.Add(pLst);
        var pExp = cmd.CreateParameter();
        pExp.ParameterName = "@expires";
        pExp.Value = session.ExpiresAt;
        cmd.Parameters.Add(pExp);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <inheritdoc />
    public async Task<AppSession?> GetAsync(string? sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("sessionId cannot be null or empty.", nameof(sessionId));

        const string sql = @"
SELECT SessionId, DeviceInfo, CreatedAt, LastSeenAt, ExpiresAt
FROM appsession
WHERE SessionId = @sid";

        await using DbConnection conn = await _factory.CreateOpenAsync(ct);
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var pSid = cmd.CreateParameter();
        pSid.ParameterName = "@sid";
        pSid.Value = sessionId;
        cmd.Parameters.Add(pSid);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            // ADO.NET générique : utilise GetOrdinal puis GetXxx par index
            int iSid = reader.GetOrdinal("SessionId");
            int iDev = reader.GetOrdinal("DeviceInfo");
            int iCrt = reader.GetOrdinal("CreatedAt");
            int iLst = reader.GetOrdinal("LastSeenAt");
            int iExp = reader.GetOrdinal("ExpiresAt");

            string sid = reader.GetString(iSid);
            string device = reader.IsDBNull(iDev) ? string.Empty : reader.GetString(iDev);
            DateTime created = reader.GetDateTime(iCrt);
            DateTime lastSeen = reader.GetDateTime(iLst);
            DateTime expires = reader.GetDateTime(iExp);

            return new AppSession(sid, device, created, lastSeen, expires);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("sessionId cannot be null or empty.", nameof(sessionId));

        const string sql = @"DELETE FROM appsession WHERE SessionId = @sid";

        await using DbConnection conn = await _factory.CreateOpenAsync(ct);
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var pSid = cmd.CreateParameter();
        pSid.ParameterName = "@sid";
        pSid.Value = sessionId;
        cmd.Parameters.Add(pSid);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string sessionId, DbConnection conn, DbTransaction tx)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("sessionId cannot be null or empty.", nameof(sessionId));
        if (conn is null) throw new ArgumentNullException(nameof(conn));
        if (tx is null) throw new ArgumentNullException(nameof(tx));

        await using DbCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM appsession WHERE SessionId = @sessionId";

        var pSid = cmd.CreateParameter();
        pSid.ParameterName = "@sessionId";
        pSid.Value = sessionId;
        cmd.Parameters.Add(pSid);

        await cmd.ExecuteNonQueryAsync();
    }
}