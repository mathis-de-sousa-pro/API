using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using API.Managers.InterfacesServices;
using API.Models;
using API.Services.Audit;
using API.Services.Masking;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace API.Services;

/// <summary>
/// Service for audit logging of authentication and business actions.
/// </summary>
public class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IAuditWriter _writer;
    private readonly IClockService _clock;
    private readonly IMaskingHelper _maskingHelper;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public AuditService(
        IAuditWriter writer,
        IClockService clock,
        IMaskingHelper maskingHelper,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _maskingHelper = maskingHelper ?? throw new ArgumentNullException(nameof(maskingHelper));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public Task LogAuthAsync(
        string provider,
        string action,
        object? metadata = null,
        string? sessionId = null,
        string? userId = null,
        CancellationToken ct = default) =>
        LogActionInternalAsync(
            sessionId,
            userId,
            $"auth.{action}",
            provider,
            metadata,
            ct);

    public Task LogActionAsync(
        string? sessionId,
        string? userId,
        string action,
        string? target,
        object? metadata,
        CancellationToken ct = default) =>
        LogActionInternalAsync(sessionId, userId, action, target, metadata, ct);

    private async Task LogActionInternalAsync(
        string? sessionId,
        string? userId,
        string action,
        string? target,
        object? metadata,
        CancellationToken ct)
    {
        bool isEnabled = IsEnabled();

        if (isEnabled)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                throw new ArgumentException("Action must be provided.", nameof(action));
            }

            AuditRecord record = new(
                ResolveCorrelationId(),
                sessionId,
                userId,
                action,
                target,
                Serialize(metadata),
                _clock.GetUtcNow());

            await _writer.EnqueueAuditAsync(record, ct).ConfigureAwait(false);
        }
    }

    private bool IsEnabled() => _configuration.GetValue("Observability:Audit:Enabled", true);

    private string ResolveCorrelationId()
    {
        HttpContext? context = _httpContextAccessor.HttpContext;
        string correlationId = Guid.NewGuid().ToString("N");

        if (context != null)
        {
            bool hasItem = context.Items.TryGetValue("CorrelationId", out object? value) && value is string existing && !string.IsNullOrWhiteSpace(existing);

            if (hasItem)
            {
                correlationId = existing;
            }
            else
            {
                string responseHeader = context.Response?.Headers["X-Correlation-Id"].ToString() ?? string.Empty;
                string requestHeader = context.Request?.Headers["X-Correlation-Id"].ToString() ?? string.Empty;
                string candidate = !string.IsNullOrWhiteSpace(responseHeader) ? responseHeader : requestHeader;

                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    correlationId = candidate;
                }
                else
                {
                    correlationId = Guid.NewGuid().ToString("N");

                    if (context.Response != null)
                    {
                        context.Response.Headers["X-Correlation-Id"] = correlationId;
                    }
                }
            }
        }

        return correlationId;
    }

    private string? Serialize(object? metadata)
    {
        string? serialized = null;

        if (metadata != null)
        {
            if (metadata is string str)
            {
                serialized = _maskingHelper.Mask(str);
            }
            else
            {
                string json = JsonSerializer.Serialize(metadata, SerializerOptions);
                serialized = _maskingHelper.Mask(json);
            }
        }

        return serialized;
    }
}
