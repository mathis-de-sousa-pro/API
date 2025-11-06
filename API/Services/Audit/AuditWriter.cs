using System.Threading.Channels;
using API.Managers.InterfacesServices;
using API.Models;

namespace API.Services.Audit;

public sealed class AuditWriter : IAuditWriter
{
    private readonly Channel<object> _channel;

    public AuditWriter()
    {
        _channel = Channel.CreateBounded<object>(new BoundedChannelOptions(5_000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public ValueTask EnqueueRequestAsync(RequestLog log, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        return _channel.Writer.WriteAsync(log, ct);
    }

    public ValueTask EnqueueErrorAsync(ErrorRecord error, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(error);
        return _channel.Writer.WriteAsync(error, ct);
    }

    public ValueTask EnqueueAuditAsync(AuditRecord audit, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(audit);
        return _channel.Writer.WriteAsync(audit, ct);
    }

    internal IAsyncEnumerable<object> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
