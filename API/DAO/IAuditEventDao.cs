using API.Models;

namespace API.DAO;

public interface IAuditEventDao
{
    Task InsertAsync(AuditRecord record, CancellationToken ct = default);
}
