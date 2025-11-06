using System.Data.Common;
using API.Managers.InterfacesServices;
using API.Models;

namespace API.DAO;

public sealed class AuditEventDao(ISqlConnectionFactory factory) : IAuditEventDao
{
    public async Task InsertAsync(AuditRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        const string sql = @"
insert into audit_event (
    CorrelationId,
    SessionId,
    UserId,
    Action,
    Target,
    MetadataJson,
    CreatedUtc)
values (
    @correlationId,
    @sessionId,
    @userId,
    @action,
    @target,
    @metadata,
    @createdUtc);";

        await using DbConnection conn = await factory.CreateOpenAsync(ct).ConfigureAwait(false);
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        Add(cmd, "@correlationId", record.CorrelationId);
        Add(cmd, "@sessionId", record.SessionId);
        Add(cmd, "@userId", record.UserId);
        Add(cmd, "@action", record.Action);
        Add(cmd, "@target", record.Target);
        Add(cmd, "@metadata", record.MetadataJson);
        Add(cmd, "@createdUtc", record.CreatedUtc);

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
