using System.Net.Http;
using System.Threading.Tasks;
using API.Errors.Exceptions;

namespace API.Errors;

/// <summary>
/// Default implementation of <see cref="IErrorMapper"/> for mapping exceptions to standardized <see cref="ApiError"/> responses.
/// Handles various known exception types and assigns appropriate error codes, messages, and HTTP status codes.
/// </summary>
public class DefaultErrorMapper : IErrorMapper
{
    private static readonly Dictionary<Type, (string Code, string DefaultMessage, int Status)> ExceptionMappings =
        new()
        {
            { typeof(InvalidStateException), ("error.invalid_state", "Invalid state.", 400) },
            { typeof(TokenExchangeFailedException), ("error.token_exchange_failed", "Token exchange failed.", 502) },
            { typeof(MissingTokenSetException), ("error.auth.missing_token", "Missing authentication token set.", 401) },
            { typeof(RecoverableSpotifyApiException), ("error.spotify.transient", "Temporary Spotify error.", 503) },
            { typeof(HttpRequestException), ("error.upstream.network", "Upstream network error.", 502) },
            { typeof(TaskCanceledException), ("error.upstream.timeout", "Upstream request timed out.", 504) },
            { typeof(ArgumentException), ("error.bad_request", "Bad request.", 400) },
            { typeof(UnauthorizedAccessException), ("error.unauthorized", "Unauthorized.", 401) },
            { typeof(NotImplementedException), ("error.not_implemented", "Not implemented.", 501) }
        };

    /// <inheritdoc />
    public ApiError Map(Exception? exception, string correlationId, bool includeDetails, DateTime nowUtc, out int httpStatus)
    {
        ApiError error;

        if (exception == null)
        {
            httpStatus = 500;
            error = new ApiError("error.unknown", "Unknown error.", correlationId, nowUtc, string.Empty);
        }
        else
        {
            string details = includeDetails ? exception.ToString() : string.Empty;
            bool hasMapping = ExceptionMappings.TryGetValue(
                exception.GetType(),
                out (string Code, string DefaultMessage, int Status) mapping);

            if (hasMapping)
            {
                httpStatus = mapping.Status;
                error = new ApiError(mapping.Code, exception.Message, correlationId, nowUtc, details);
            }
            else
            {
                httpStatus = 500;
                error = new ApiError("error.unhandled", "An unexpected error occurred.", correlationId, nowUtc, details);
            }
        }

        return error;
    }
}
