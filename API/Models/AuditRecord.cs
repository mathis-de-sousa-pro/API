namespace API.Models;

public sealed record AuditRecord(
    string CorrelationId,
    string? SessionId,
    string? UserId,
    string Action,
    string? Target,
    string? MetadataJson,
    DateTime CreatedUtc);
