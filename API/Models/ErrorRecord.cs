namespace API.Models;

public sealed record ErrorRecord(
    string CorrelationId,
    string? SessionId,
    string? UserId,
    string ExceptionType,
    string Code,
    string Message,
    int HttpStatus,
    string? Path,
    string? Method,
    string? DetailsTruncated,
    DateTime CreatedUtc);
