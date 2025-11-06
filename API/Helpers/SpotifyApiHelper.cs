#nullable enable

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using API.DTO;
using API.Errors.Exceptions;
using API.Managers.InterfacesHelpers;
using API.Managers.InterfacesServices;

namespace API.Helpers;

/// <summary>
/// Helper class for interacting with the Spotify Web API.
/// Provides methods to retrieve playlists for a user.
/// </summary>
public class SpotifyApiHelper(HttpClient http, IConfigService config) : ISpotifyApiHelper
{
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));
    private readonly IConfigService _config = config ?? throw new ArgumentNullException(nameof(config));
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public async Task<PlaylistPageDto> GetPlaylistsAsync(string accessToken, string? pageToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("accessToken cannot be null or empty.", nameof(accessToken));

        string url = BuildPlaylistsUrl(pageToken);

        using HttpRequestMessage request = CreateAuthRequest(url, accessToken);
        using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using Stream contentStream = await response.Content.ReadAsStreamAsync(ct);
        SpotifyPlaylistsResponse? json = await DeserializeJsonAsync<SpotifyPlaylistsResponse>(contentStream, ct);

        if (json is null)
            throw new InvalidOperationException("Failed to deserialize Spotify playlists response.");

        return MapToPlaylistPageDto(json);
    }

    /// <summary>
    /// Builds the playlists endpoint URL, preserving offsets if the provided token is relative.
    /// </summary>
    /// <param name="pageToken">The supplied page token or next URL from Spotify.</param>
    /// <returns>Fully qualified request path.</returns>
    private string BuildPlaylistsUrl(string? pageToken)
    {
        if (!string.IsNullOrWhiteSpace(pageToken) && Uri.IsWellFormedUriString(pageToken, UriKind.Absolute))
            return pageToken;

        int limit = _config.GetSpotifyPlaylistsPageSize();
        string offsetParam = string.Empty;
        if (!string.IsNullOrWhiteSpace(pageToken) && int.TryParse(pageToken, out int offset) && offset >= 0)
            offsetParam = $"&offset={offset}";

        return $"me/playlists?limit={limit}{offsetParam}";
    }

    /// <summary>
    /// Creates an authenticated GET request with the supplied bearer token.
    /// </summary>
    /// <param name="url">Relative or absolute request URL.</param>
    /// <param name="accessToken">Bearer token provided by Spotify.</param>
    /// <returns>A configured <see cref="HttpRequestMessage"/>.</returns>
    private HttpRequestMessage CreateAuthRequest(string url, string accessToken)
    {
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    /// <summary>
    /// Deserializes JSON content using the shared serializer options.
    /// </summary>
    /// <typeparam name="T">Target type.</typeparam>
    /// <param name="stream">Content stream.</param>
    /// <param name="ct">Cancellation token.</param>
    private static async Task<T?> DeserializeJsonAsync<T>(Stream stream, CancellationToken ct = default)
    {
        return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, ct);
    }

    /// <summary>
    /// Maps a Spotify playlists response into the API DTO model.
    /// </summary>
    /// <param name="json">Spotify response payload.</param>
    /// <returns>DTO populated with playlist items.</returns>
    private static PlaylistPageDto MapToPlaylistPageDto(SpotifyPlaylistsResponse json)
    {
        List<PlaylistItemDto> items = json.Items.Select(i => new PlaylistItemDto
            {
                PlaylistId = i.Id ?? string.Empty,
                Name = i.Name ?? string.Empty,
                ImageUrl = i.Images?.FirstOrDefault()?.Url,
                Owner = i.Owner?.DisplayName ?? i.Owner?.Id,
                TrackCount = i.Tracks.Total ?? 0,
                Selected = false
            }
        ).ToList();

        return new PlaylistPageDto
        {
            Items = items,
            NextPageToken = json.Next
        };
    }

    /// <inheritdoc />
    public async Task<PlaylistTracksDTO> GetPlaylistTracksAsync(string accessToken, string playlistId, int? offset,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("accessToken cannot be null or empty.", nameof(accessToken));
        if (string.IsNullOrWhiteSpace(playlistId))
            throw new ArgumentException("playlistId cannot be null or empty.", nameof(playlistId));

        string url = $"playlists/{playlistId}/tracks?limit={_config.GetSpotifyPlaylistsPageSize()}&offset={offset}";

        using HttpRequestMessage request = CreateAuthRequest(url, accessToken);
        using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using Stream responseStream = await response.Content.ReadAsStreamAsync(ct);
        PlaylistTracksResponse? spotifyResponse = await DeserializeJsonAsync<PlaylistTracksResponse>(responseStream, ct);

        if (spotifyResponse is null)
            throw new InvalidOperationException("Failed to deserialize Spotify playlist tracks response.");

        List<SpotifyTrack> tracks = spotifyResponse.Items
            .Where(i => i.Track is not null)
            .Select(i => new SpotifyTrack
                {
                    Id = i.Track.Id ?? string.Empty,
                    Name = i.Track.Name ?? string.Empty,
                    Artists = i.Track.Artists?.Select(a => new ArtistDTO
                        {
                            Id = a.Id,
                            Name = a.Name
                        }
                    ).ToList() ?? new List<ArtistDTO>(),
                    Album = i.Track.Album is not null
                        ? new AlbumDTO
                        {
                            Id = i.Track.Album.Id,
                            Images = i.Track.Album.Images?
                                .Select(img => new SpotifyImage { Url = img.Url })
                                .ToList() ?? new List<SpotifyImage>()
                        }
                        : throw new InvalidOperationException("Track is missing album metadata.")
                }
            ).ToList();

        return new PlaylistTracksDTO
        {
            PlaylistId = playlistId,
            Limit = spotifyResponse.Limit,
            Offset = spotifyResponse.Offset,
            Tracks = tracks
        };
    }

    /// <inheritdoc />
    public async Task<int> GetSavedTracksTotalAsync(string accessToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("accessToken cannot be null or empty.", nameof(accessToken));

        const string url = "me/tracks?limit=1&offset=0";
        using HttpRequestMessage request = CreateAuthRequest(url, accessToken);
        using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new UnauthorizedAccessException("Spotify access token is invalid or expired.");

            int statusCode = (int)response.StatusCode;
            if (response.StatusCode == (HttpStatusCode)429 || (statusCode >= 500 && statusCode <= 599))
                throw new RecoverableSpotifyApiException(
                    $"Spotify returned {(int)response.StatusCode} ({response.StatusCode}) when fetching saved tracks total."
                );

            throw new HttpRequestException(
                $"Failed to retrieve saved tracks total. Status code: {(int)response.StatusCode} ({response.StatusCode})."
            );
        }

        await using Stream responseStream = await response.Content.ReadAsStreamAsync(ct);
        using JsonDocument doc = await JsonDocument.ParseAsync(responseStream, cancellationToken: ct);

        if (doc.RootElement.TryGetProperty("total", out JsonElement totalElement) && totalElement.TryGetInt32(out int total))
            return total;

        throw new InvalidOperationException("Spotify saved tracks response did not contain a total value.");
    }
}
