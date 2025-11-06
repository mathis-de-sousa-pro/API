using System.Threading;
using System.Threading.Tasks;

namespace API.Managers.InterfacesServices;

public interface IAuditService
{
    Task LogAuthAsync(
        string provider,
        string action,
        object? metadata = null,
        string? sessionId = null,
        string? userId = null,
        CancellationToken ct = default);

    Task LogActionAsync(
        string? sessionId,
        string? userId,
        string action,
        string? target,
        object? metadata,
        CancellationToken ct = default);
}
