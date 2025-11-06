using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using API.Managers.InterfacesServices;
using API.Models;

namespace API.DAO;

public sealed class RequestLogDao(ISqlConnectionFactory factory) : IRequestLogDao
{
    public async Task InsertAsync(RequestLog log, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(log);

        const string sql = @"
insert into request_log (
    CorrelationId,
    SessionId,
    UserId,
    ProviderUserId,
    Path,
    Method,
    Status,
    LatencyMs,
    RequestBytes,
    ResponseBytes,
    RemoteIp,
    UserAgent,
    CreatedUtc)
values (
    @correlationId,
    @sessionId,
    @userId,
    @providerUserId,
    @path,
    @method,
    @status,
    @latencyMs,
    @requestBytes,
    @responseBytes,
    @remoteIp,
    @userAgent,
    @createdUtc);";

        await using DbConnection conn = await factory.CreateOpenAsync(ct).ConfigureAwait(false);
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        Add(cmd, "@correlationId", log.CorrelationId);
        Add(cmd, "@sessionId", log.SessionId);
        Add(cmd, "@userId", log.UserId);
        Add(cmd, "@providerUserId", log.ProviderUserId);
        Add(cmd, "@path", log.Path);
        Add(cmd, "@method", log.Method);
        Add(cmd, "@status", log.Status);
        Add(cmd, "@latencyMs", log.LatencyMs);
        Add(cmd, "@requestBytes", log.RequestBytes);
        Add(cmd, "@responseBytes", log.ResponseBytes);
        Add(cmd, "@remoteIp", log.RemoteIp);
        Add(cmd, "@userAgent", log.UserAgent);
        Add(cmd, "@createdUtc", log.CreatedUtc);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static void Add(DbCommand cmd, string name, object? value)
    {
        DbParameter parameter = cmd.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(parameter);
    }
}
