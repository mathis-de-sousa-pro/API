using API.Models;

namespace API.DAO;

public interface IErrorEventDao
{
    Task InsertAsync(ErrorRecord record, CancellationToken ct = default);
}
