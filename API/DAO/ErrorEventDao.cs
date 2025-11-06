using System.Data.Common;
using API.Managers.InterfacesServices;
using API.Models;

namespace API.DAO;

public sealed class ErrorEventDao(ISqlConnectionFactory factory) : IErrorEventDao
{
    public async Task InsertAsync(ErrorRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        const string sql = @"
insert into error_event (
    CorrelationId,
    SessionId,
    UserId,
    ExceptionType,
    Code,
    Message,
    HttpStatus,
    Path,
    Method,
    DetailsTrunc,
    CreatedUtc)
values (
    @correlationId,
    @sessionId,
    @userId,
    @exceptionType,
    @code,
    @message,
    @httpStatus,
    @path,
    @method,
    @details,
    @createdUtc);";

        await using DbConnection conn = await factory.CreateOpenAsync(ct).ConfigureAwait(false);
        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        Add(cmd, "@correlationId", record.CorrelationId);
        Add(cmd, "@sessionId", record.SessionId);
        Add(cmd, "@userId", record.UserId);
        Add(cmd, "@exceptionType", record.ExceptionType);
        Add(cmd, "@code", record.Code);
        Add(cmd, "@message", record.Message);
        Add(cmd, "@httpStatus", record.HttpStatus);
        Add(cmd, "@path", record.Path);
        Add(cmd, "@method", record.Method);
        Add(cmd, "@details", record.DetailsTruncated);
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
