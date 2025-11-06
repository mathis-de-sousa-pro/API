namespace API.Models;

public sealed record RequestLog(
    string CorrelationId,
    string? SessionId,
    string? UserId,
    string? ProviderUserId,
    string Path,
    string Method,
    int Status,
    int LatencyMs,
    int? RequestBytes,
    int? ResponseBytes,
    string? RemoteIp,
    string? UserAgent,
    DateTime CreatedUtc);
