using System.Diagnostics;
using API.Managers.InterfacesServices;
using API.Models;
using API.Services.Masking;

namespace API.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAuditWriter _auditWriter;
    private readonly IMaskingHelper _maskingHelper;
    private readonly bool _enabled;
    private readonly double _sampleRate;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        IAuditWriter auditWriter,
        IMaskingHelper maskingHelper,
        IConfiguration configuration)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
        _maskingHelper = maskingHelper ?? throw new ArgumentNullException(nameof(maskingHelper));
        ArgumentNullException.ThrowIfNull(configuration);

        _enabled = configuration.GetValue("Observability:RequestLogging:Enabled", true);
        _sampleRate = Math.Clamp(configuration.GetValue("Observability:RequestLogging:SampleRate", 1.0), 0.0, 1.0);
    }

    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Stopwatch? watch = null;
        int? requestBytes = null;

        if (_enabled)
        {
            watch = Stopwatch.StartNew();
            requestBytes = context.Request.ContentLength.HasValue
                ? SafeToInt(context.Request.ContentLength.Value)
                : null;
        }

        await _next(context).ConfigureAwait(false);

        if (_enabled && watch is not null)
        {
            watch.Stop();
            bool shouldLog = ShouldSample();

            if (shouldLog)
            {
                string correlationId = context.Response.Headers["X-Correlation-Id"];

                if (string.IsNullOrEmpty(correlationId))
                {
                    string traceIdentifier = context.TraceIdentifier?.Replace("-", string.Empty, StringComparison.Ordinal) ?? Guid.NewGuid().ToString("N");
                    correlationId = traceIdentifier;
                    context.Response.Headers["X-Correlation-Id"] = correlationId;
                }

                int? responseBytes = context.Response.ContentLength.HasValue
                    ? SafeToInt(context.Response.ContentLength.Value)
                    : null;

                RequestLog log = new(
                    correlationId,
                    TryGetItem(context, "SessionId"),
                    TryGetItem(context, "UserId"),
                    TryGetItem(context, "ProviderUserId"),
                    context.Request.Path.HasValue ? context.Request.Path.Value! : string.Empty,
                    context.Request.Method,
                    context.Response.StatusCode,
                    SafeToInt(watch.ElapsedMilliseconds),
                    requestBytes,
                    responseBytes,
                    context.Connection.RemoteIpAddress?.ToString(),
                    _maskingHelper.Mask(context.Request.Headers["User-Agent"].ToString()),
                    DateTime.UtcNow);

                await _auditWriter.EnqueueRequestAsync(log).ConfigureAwait(false);
            }
        }
    }

    private bool ShouldSample()
    {
        return _sampleRate >= 1.0 || Random.Shared.NextDouble() <= _sampleRate;
    }

    private static int SafeToInt(long value)
    {
        int result;

        if (value <= int.MinValue)
        {
            result = int.MinValue;
        }
        else if (value >= int.MaxValue)
        {
            result = int.MaxValue;
        }
        else
        {
            result = (int)value;
        }

        return result;
    }

    private static string? TryGetItem(HttpContext context, string key)
    {
        string? result = null;

        if (context.Items.TryGetValue(key, out object? value) && value is not null)
        {
            result = value.ToString();
        }

        return result;
    }
}
