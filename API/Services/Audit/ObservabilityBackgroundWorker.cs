using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using API.DAO;
using API.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Services.Audit;

public sealed class ObservabilityBackgroundWorker : BackgroundService
{
    private readonly AuditWriter _writer;
    private readonly IRequestLogDao _requestDao;
    private readonly IErrorEventDao _errorDao;
    private readonly IAuditEventDao _auditDao;
    private readonly ILogger<ObservabilityBackgroundWorker> _logger;

    public ObservabilityBackgroundWorker(
        AuditWriter writer,
        IRequestLogDao requestDao,
        IErrorEventDao errorDao,
        IAuditEventDao auditDao,
        ILogger<ObservabilityBackgroundWorker> logger)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _requestDao = requestDao ?? throw new ArgumentNullException(nameof(requestDao));
        _errorDao = errorDao ?? throw new ArgumentNullException(nameof(errorDao));
        _auditDao = auditDao ?? throw new ArgumentNullException(nameof(auditDao));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (object item in _writer.ReadAllAsync(stoppingToken))
        {
            try
            {
                switch (item)
                {
                    case RequestLog requestLog:
                        await _requestDao.InsertAsync(requestLog, stoppingToken).ConfigureAwait(false);
                        break;
                    case ErrorRecord errorRecord:
                        await _errorDao.InsertAsync(errorRecord, stoppingToken).ConfigureAwait(false);
                        break;
                    case AuditRecord auditRecord:
                        await _auditDao.InsertAsync(auditRecord, stoppingToken).ConfigureAwait(false);
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
