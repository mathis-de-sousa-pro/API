using System.Text.Json;
using API.Errors;
using API.Managers.InterfacesServices;
using API.Models;
using API.Services.Masking;

namespace API.Middleware;

/// <summary>
/// Middleware for centralized error handling in HTTP requests.
/// Captures unhandled exceptions, logs them, maps them to API errors, and returns a standardized JSON response.
/// </summary>
public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IErrorMapper _mapper;
    private readonly IClockService _clock;
    private readonly IHostEnvironment _environment;
    private readonly IAuditWriter _auditWriter;
    private readonly IMaskingHelper _maskingHelper;
    private readonly IConfiguration _configuration;

    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlingMiddleware> logger,
        IErrorMapper mapper,
        IClockService clock,
        IHostEnvironment environment,
        IAuditWriter auditWriter,
        IMaskingHelper maskingHelper,
        IConfiguration configuration)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
        _maskingHelper = maskingHelper ?? throw new ArgumentNullException(nameof(maskingHelper));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Handles the HTTP request, catching and processing any unhandled exceptions.
    /// Logs the error, maps it to an API error, and writes a JSON response with appropriate status code and correlation ID.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string correlationId = GetOrCreateCorrelationId(context);
        context.Items["CorrelationId"] = correlationId;

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            bool includeDetails = _environment.IsDevelopment();
            DateTime now = _clock.GetUtcNow();

            ApiError apiError = _mapper.Map(ex, correlationId, includeDetails, now, out int status);

            _logger.LogError(ex, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            context.Response.Headers["X-Correlation-Id"] = correlationId;

            JsonSerializerOptions options = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            string payload = JsonSerializer.Serialize(apiError, options);
            await context.Response.WriteAsync(payload).ConfigureAwait(false);

            if (_configuration.GetValue("Observability:ErrorPersistence:Enabled", true))
            {
                int maxDetail = _configuration.GetValue("Observability:ErrorPersistence:MaxDetail", 1500);
                string? sessionId = TryGetItem(context, "SessionId");
                string? userId = TryGetItem(context, "UserId");

                ErrorRecord record = new(
                    correlationId,
                    sessionId,
                    userId,
                    ex.GetType().FullName ?? ex.GetType().Name,
                    apiError.Code,
                    _maskingHelper.Mask(apiError.Message ?? string.Empty),
                    status,
                    context.Request.Path.HasValue ? context.Request.Path.Value! : string.Empty,
                    context.Request.Method,
                    includeDetails ? _maskingHelper.Truncate(ex.ToString(), maxDetail) : null,
                    now);

                await _auditWriter.EnqueueErrorAsync(record).ConfigureAwait(false);
            }
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        string requestHeader = context.Request.Headers["X-Correlation-Id"]!;
        string correlationId = requestHeader;

        if (string.IsNullOrWhiteSpace(requestHeader))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.Response.Headers["X-Correlation-Id"] = correlationId;

        return correlationId;
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
