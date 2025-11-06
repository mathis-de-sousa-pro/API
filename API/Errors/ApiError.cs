namespace API.Errors;

/// <summary>
/// Represents a standardized API error with code, message, correlation ID, timestamp, and details.
/// </summary>
/// <param name="code">The error code (must not be null or whitespace; defaults to "error" if invalid).</param>
/// <param name="message">The error message (must not be null; defaults to empty string if null).</param>
/// <param name="correlationId">The correlation identifier for tracing the error (must not be null; defaults to empty string if null).</param>
/// <param name="timestamp">The timestamp when the error occurred.</param>
/// <param name="details">Additional details about the error (must not be null; defaults to empty string if null).</param>
public class ApiError(string code, string message, string correlationId, DateTime timestamp, string details)
{
    private string _code = code;
    private string _message = message;
    private string _correlationId = correlationId;
    private DateTime _timestamp = timestamp;
    private string _details = details;

    /// <summary>
    /// Gets or sets the error code.
    /// If set to null or whitespace, defaults to "error".
    /// </summary>
    public string Code
    {
        get => _code;
        set => _code = string.IsNullOrWhiteSpace(value) ? "error" : value;
    }

    /// <summary>
    /// Gets or sets the error message.
    /// If set to null, defaults to an empty string.
    /// </summary>
    public string? Message
    {
        get => _message;
        set => _message = value ?? string.Empty;
    }

    /// <summary>
    /// Gets or sets the correlation identifier for tracing the error.
    /// If set to null, defaults to an empty string.
    /// </summary>
    public string? CorrelationId
    {
        get => _correlationId;
        set => _correlationId = value ?? string.Empty;
    }

    /// <summary>
    /// Gets or sets the timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp
    {
        get => _timestamp;
        set => _timestamp = value;
    }

    /// <summary>
    /// Gets or sets additional details about the error.
    /// If set to null, defaults to an empty string.
    /// </summary>
    public string? Details
    {
        get => _details;
        set => _details = value ?? string.Empty;
    }
}