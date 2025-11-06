using API.DAO;
using API.Models;

namespace API.Services.Audit;

public sealed class ObservabilityBackgroundWorker : BackgroundService
{
    private readonly AuditWriter _writer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ObservabilityBackgroundWorker> _logger;

    public ObservabilityBackgroundWorker(
        AuditWriter writer,
        IServiceScopeFactory scopeFactory,
        ILogger<ObservabilityBackgroundWorker> logger)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (object item in _writer.ReadAllAsync(stoppingToken))
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IServiceProvider services = scope.ServiceProvider;

                switch (item)
                {
                    case RequestLog requestLog:
                        await services
                            .GetRequiredService<IRequestLogDao>()
                            .InsertAsync(requestLog, stoppingToken)
                            .ConfigureAwait(false);
                        break;
                    case ErrorRecord errorRecord:
                        await services
                            .GetRequiredService<IErrorEventDao>()
                            .InsertAsync(errorRecord, stoppingToken)
                            .ConfigureAwait(false);
                        break;
                    case AuditRecord auditRecord:
                        await services
                            .GetRequiredService<IAuditEventDao>()
                            .InsertAsync(auditRecord, stoppingToken)
                            .ConfigureAwait(false);
                        break;
                    default:
                        _logger.LogWarning("Unknown observability payload type {Type}", item.GetType().Name);
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist observability event of type {Type}", item.GetType().Name);
            }
        }
    }
}
