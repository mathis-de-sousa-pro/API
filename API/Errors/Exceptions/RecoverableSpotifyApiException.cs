namespace API.Errors.Exceptions;

/// <summary>
/// Represents a transient Spotify API failure where the operation can be safely retried later.
/// </summary>
public class RecoverableSpotifyApiException(string message) : Exception(message);
