namespace API.Errors;

/// <summary>
/// Defines a contract for mapping exceptions to standardized <see cref="ApiError"/> responses.
/// </summary>
public interface IErrorMapper
{
    /// <summary>
    /// Maps an <see cref="Exception"/> to an <see cref="ApiError"/> and determines the corresponding HTTP status code.
    /// </summary>
    /// <param name="exception">The exception to map. Can be <c>null</c> to represent an unknown error.</param>
    /// <param name="correlationId">A correlation identifier for tracing the error. Can be <c>null</c>.</param>
    /// <param name="includeDetails">If <c>true</c>, includes detailed exception information in the error response.</param>
    /// <param name="nowUtc">The current UTC timestamp to use for the error.</param>
    /// <param name="httpStatus">When this method returns, contains the HTTP status code corresponding to the error.</param>
    /// <returns>
    /// An <see cref="ApiError"/> representing the mapped error.
    /// </returns>
    ApiError Map(Exception? exception, string correlationId, bool includeDetails, DateTime nowUtc, out int httpStatus);
}