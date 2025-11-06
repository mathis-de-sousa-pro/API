using System.Text.Json;
using API.Controllers.InterfacesManagers;
using API.DTO;
using API.Errors.Exceptions;
using Api.Managers.InterfacesDao;
using Api.Managers.InterfacesHelpers;
using API.Managers.InterfacesHelpers;
using API.Managers.InterfacesServices;
using Api.Models;

namespace API.Managers;

/// <summary>
/// Implements the playlist listing flow with DB cache and token refresh.
/// Pure orchestration: no SQL (DAOs only) and no HTTP (Helpers only).
/// </summary>
public sealed class UserDataManager(
    ITokenDao tokenDao,
    IAccessTokenDao accessTokenDao,
    IPlaylistCacheDao playlistCacheDao,
    ISpotifyOAuthHelper spotifyOAuthHelper,
    ISpotifyApiHelper spotifyApiHelper,
    IClockService clock,
    IConfigService config,
    ILogger<UserDataManager> logger)
    : IUserDataManager
{
    private readonly ITokenDao _tokenDao = tokenDao ?? throw new ArgumentNullException(nameof(tokenDao));

    private readonly IAccessTokenDao _accessTokenDao =
        accessTokenDao ?? throw new ArgumentNullException(nameof(accessTokenDao));

    private readonly IPlaylistCacheDao _playlistCacheDao =
        playlistCacheDao ?? throw new ArgumentNullException(nameof(playlistCacheDao));

    private readonly ISpotifyOAuthHelper _spotifyOAuthHelper =
        spotifyOAuthHelper ?? throw new ArgumentNullException(nameof(spotifyOAuthHelper));

    private readonly ISpotifyApiHelper _spotifyApiHelper =
        spotifyApiHelper ?? throw new ArgumentNullException(nameof(spotifyApiHelper));

    private readonly IClockService _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly IConfigService _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly ILogger<UserDataManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public async Task<PlaylistPageDto> GetPlaylistsAsync(string sessionId, string? pageToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("sessionId cannot be null or empty.", nameof(sessionId));

        DateTime nowUtc = _clock.GetUtcNow();
        string normalizedPageToken = pageToken ?? string.Empty;

        TokenSet? tokenSet = await _tokenDao.GetBySessionAsync(sessionId);
        if (tokenSet is null)
            throw new MissingTokenSetException("No TokenSet associated with the given session.");

        string providerUserId = tokenSet.ProviderUserId;
        string? accessToken = await _accessTokenDao.GetValidBySessionAsync(sessionId, nowUtc, ct);

        if (string.IsNullOrEmpty(accessToken))
        {
            string refreshToken = tokenSet.RefreshToken;
            RefreshResult refreshed = await _spotifyOAuthHelper.RefreshTokensAsync(refreshToken, ct);
            accessToken = refreshed.AccessToken;

            await _accessTokenDao.UpsertAsync(sessionId, accessToken, refreshed.AccessExpiresAtUtc, nowUtc, ct);

            string effectiveRefresh = string.IsNullOrEmpty(refreshed.NewRefreshToken)
                ? refreshToken
                : refreshed.NewRefreshToken!;
            await _tokenDao.UpdateAfterRefreshAsync(sessionId, effectiveRefresh, refreshed.AccessExpiresAtUtc, ct);
        }

        int? savedTracksTotal = null;
        try
        {
            savedTracksTotal = await _spotifyApiHelper.GetSavedTracksTotalAsync(accessToken, ct);
        }
        catch (RecoverableSpotifyApiException ex)
        {
            _logger.LogDebug(ex, "Skipping liked songs pseudo-playlist due to recoverable Spotify API error.");
        }

        PlaylistPageDto result;
        string? cachedJson = await _playlistCacheDao.GetPageJsonAsync(sessionId, normalizedPageToken, nowUtc, ct);

        if (!string.IsNullOrEmpty(cachedJson))
        {
            PlaylistPageDto? cachedPage = JsonSerializer.Deserialize<PlaylistPageDto>(cachedJson);
            result = cachedPage ?? new PlaylistPageDto();
        }
        else
        {
            PlaylistPageDto livePage = await _spotifyApiHelper.GetPlaylistsAsync(accessToken, normalizedPageToken, ct);

            int ttlMinutes = _config.GetPlaylistCacheTtlMinutes();
            DateTime expiresAtUtc = nowUtc.AddMinutes(ttlMinutes);
            string rawJson = JsonSerializer.Serialize(livePage);

            await _playlistCacheDao.UpsertPageAsync(
                sessionId,
                providerUserId,
                normalizedPageToken,
                rawJson,
                expiresAtUtc,
                nowUtc,
                ct
            );

            result = livePage;
        }

        result = EnsureItemsList(result);

        if (savedTracksTotal.HasValue)
        {
            const string likedPlaylistId = "liked-saved-tracks";
            bool alreadyPresent = result.Items.Any(
                i => string.Equals(i.PlaylistId, likedPlaylistId, StringComparison.OrdinalIgnoreCase)
            );

            if (!alreadyPresent)
            {
                var liked = new PlaylistItemDto
                {
                    PlaylistId = likedPlaylistId,
                    Name = "Liked Songs",
                    ImageUrl = null,
                    Owner = providerUserId,
                    TrackCount = savedTracksTotal.Value,
                    Selected = false
                };

                result.Items.Insert(0, liked);
                _logger.LogDebug(
                    "Prepended liked songs pseudo-playlist with {TrackCount} tracks for user {ProviderUserId}.",
                    savedTracksTotal.Value,
                    providerUserId
                );
            }
        }

        return result;
    }

    private static PlaylistPageDto EnsureItemsList(PlaylistPageDto page)
    {
        if (page.Items is not null)
            return page;

        return new PlaylistPageDto
        {
            Items = new List<PlaylistItemDto>(),
            NextPageToken = page.NextPageToken
        };
    }
}
