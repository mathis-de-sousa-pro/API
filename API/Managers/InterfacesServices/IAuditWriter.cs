using API.Models;

namespace API.Managers.InterfacesServices;

public interface IAuditWriter
{
    ValueTask EnqueueRequestAsync(RequestLog log, CancellationToken ct = default);

    ValueTask EnqueueErrorAsync(ErrorRecord error, CancellationToken ct = default);

    ValueTask EnqueueAuditAsync(AuditRecord audit, CancellationToken ct = default);
}
