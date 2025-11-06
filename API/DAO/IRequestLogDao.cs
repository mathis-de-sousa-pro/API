using API.Models;

namespace API.DAO;

public interface IRequestLogDao
{
    Task InsertAsync(RequestLog log, CancellationToken ct = default);
}
