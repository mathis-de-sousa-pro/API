using API.Controllers.InterfacesManagers;
using API.DTO;
using API.Managers.InterfacesHelpers;
using API.Services;
using Microsoft.Extensions.Caching.Memory;

namespace API.Managers;

/// <summary>
/// Operations from IPlaylistManager
/// </summary>
public class PlaylistManager : IPlaylistManager
{
    private readonly ITokenService _tokenService;
    private readonly ISpotifyApiHelper _spotifyApiHelper;
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="tokenService">Token service for access token</param>
    /// <param name="spotifyApiHelper">Call API needed</param>
    /// <param name="memoryCache"></param>
    /// <exception cref="ArgumentNullException">None of these parameters should be null</exception>
    public PlaylistManager(ITokenService tokenService, ISpotifyApiHelper spotifyApiHelper, IMemoryCache memoryCache)
    {
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _spotifyApiHelper = spotifyApiHelper ?? throw new ArgumentNullException(nameof(spotifyApiHelper));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    /// <inheritdoc cref="IPlaylistManager.GetTracksByPlaylist"/>
    public async Task<PlaylistTracksDTO> GetTracksByPlaylist(
        string sessionId, 
        string playlistId, 
        int? offset, 
        CancellationToken ct = default)
    {
        string cacheKey = $"playlist_offset_{sessionId}_{playlistId}";
        int currentOffset = offset ?? _memoryCache.Get<int?>(cacheKey) ?? 0;
        string accessToken = await _tokenService.GetAccessTokenAsync(sessionId, ct);
        PlaylistTracksDTO result = await _spotifyApiHelper.GetPlaylistTracksAsync(accessToken, playlistId, currentOffset, ct);

        int? nextOffset = currentOffset + result.Limit;

        _memoryCache.Set(cacheKey, nextOffset, TimeSpan.FromDays(2));

        result.Offset = currentOffset;
        result.NextOffset = nextOffset;
        
        return result;
    }

}