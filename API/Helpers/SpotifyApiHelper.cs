using System.Net;
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

    /// <inheritdoc />
    public async Task<PlaylistPageDto> GetPlaylistsAsync(string accessToken, string? pageToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("accessToken cannot be null or empty.", nameof(accessToken));

        string url = BuildPlaylistsUrl(pageToken);

        using HttpRequestMessage req = CreateAuthRequest(url, accessToken);
        using HttpResponseMessage resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using Stream stream = await resp.Content.ReadAsStreamAsync(ct);
        var json = await DeserializeJsonAsync<SpotifyPlaylistsResponse>(stream, ct);

        if (json is null)
            throw new InvalidOperationException("Failed to deserialize Spotify playlists response.");

        return MapToPlaylistPageDto(json);
    }

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

    private HttpRequestMessage CreateAuthRequest(string url, string accessToken)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return req;
    }

    private static async Task<T?> DeserializeJsonAsync<T>(Stream stream, CancellationToken ct = default)
    {
        return await JsonSerializer.DeserializeAsync<T>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            ct
        );
    }

    private static PlaylistPageDto MapToPlaylistPageDto(SpotifyPlaylistsResponse json)
    {
        var items = json.Items.Select(i => new PlaylistItemDto
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

    /// <inheritdoc/>
    public async Task<PlaylistTracksDTO> GetPlaylistTracks(string accessToken, string playlistId, int? offset,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("accessToken cannot be null or empty.", nameof(accessToken));
        if (string.IsNullOrWhiteSpace(playlistId))
            throw new ArgumentException("playlistId cannot be null or empty.", nameof(playlistId));

        string url = $"playlists/{playlistId}/tracks?limit={_config.GetSpotifyPlaylistsPageSize()}&offset={offset}";

        using HttpRequestMessage req = CreateAuthRequest(url, accessToken);
        using HttpResponseMessage resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var spotifyResponse = await DeserializeJsonAsync<PlaylistTracksResponse>(stream, ct);

        if (spotifyResponse == null)
            throw new InvalidOperationException("Failed to deserialize Spotify playlist tracks response.");

        List<SpotifyTrack> tracks = spotifyResponse.Items
            .Where(i => i.Track != null)
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
                    Album = i.Track.Album != null
                        ? new AlbumDTO
                        {
                            Id = i.Track.Album.Id,
                            Images = i.Track.Album.Images?
                                .Select(img => new SpotifyImage() { Url = img.Url })
                                .ToList() ?? new List<SpotifyImage>()
                        }
                        : throw new NullReferenceException("None album for this track")
                }
            ).ToList();

        return new PlaylistTracksDTO()
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
        using HttpRequestMessage req = CreateAuthRequest(url, accessToken);
        using HttpResponseMessage resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!resp.IsSuccessStatusCode)
        {
            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new UnauthorizedAccessException("Spotify access token is invalid or expired.");

            int statusCode = (int)resp.StatusCode;
            if (resp.StatusCode == (HttpStatusCode)429 || (statusCode >= 500 && statusCode <= 599))
                throw new RecoverableSpotifyApiException(
                    $"Spotify returned {(int)resp.StatusCode} ({resp.StatusCode}) when fetching saved tracks total."
                );

            throw new HttpRequestException(
                $"Failed to retrieve saved tracks total. Status code: {(int)resp.StatusCode} ({resp.StatusCode})."
            );
        }

        await using Stream stream = await resp.Content.ReadAsStreamAsync(ct);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (doc.RootElement.TryGetProperty("total", out JsonElement totalElement) && totalElement.TryGetInt32(out int total))
            return total;

        throw new InvalidOperationException("Spotify saved tracks response did not contain a total value.");
    }
}
