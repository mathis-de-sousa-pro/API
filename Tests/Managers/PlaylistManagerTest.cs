using API.DTO;
using API.Managers;
using API.Managers.InterfacesHelpers;
using API.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace Tests.Managers;

public class PlaylistManagerTests
{
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ISpotifyApiHelper> _spotifyApiHelperMock;
    private readonly IMemoryCache _memoryCache;
    private readonly PlaylistManager _playlistManager;

    public PlaylistManagerTests()
    {
        _tokenServiceMock = new Mock<ITokenService>();
        _spotifyApiHelperMock = new Mock<ISpotifyApiHelper>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        _playlistManager = new PlaylistManager(_tokenServiceMock.Object, _spotifyApiHelperMock.Object, _memoryCache);
    }

    [Fact]
    public async Task GetTracksByPlaylist_ShouldFetchTracksAndSetNextOffset()
    {
        const string sessionId = "session123";
        const string playlistId = "playlistABC";
        const string accessToken = "tokenXYZ";

        PlaylistTracksDTO expectedDto = new PlaylistTracksDTO
        {
            PlaylistId = playlistId,
            Limit = 50
        };

        _tokenServiceMock
            .Setup(t => t.GetAccessTokenAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessToken);

        _spotifyApiHelperMock
            .Setup(s => s.GetPlaylistTracksAsync(accessToken, playlistId, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        PlaylistTracksDTO result = await _playlistManager.GetTracksByPlaylist(sessionId, playlistId, null);

        result.Should().NotBeNull();
        result.PlaylistId.Should().Be(playlistId);
        result.Offset.Should().Be(0);
        result.NextOffset.Should().Be(50);
        result.Limit.Should().Be(50);

        _memoryCache.TryGetValue($"playlist_offset_{sessionId}_{playlistId}", out int cachedOffset);
        cachedOffset.Should().Be(50);

        _tokenServiceMock.Verify(t => t.GetAccessTokenAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
        _spotifyApiHelperMock.Verify(s => s.GetPlaylistTracksAsync(accessToken, playlistId, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTracksByPlaylist_ShouldUseProvidedOffset_WhenOffsetIsGiven()
    {
        const string sessionId = "session123";
        const string playlistId = "playlistABC";
        const string accessToken = "tokenXYZ";
        const int customOffset = 100;

        PlaylistTracksDTO expectedDto = new PlaylistTracksDTO { PlaylistId = playlistId, Limit = 25 };

        _tokenServiceMock
            .Setup(t => t.GetAccessTokenAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessToken);

        _spotifyApiHelperMock
            .Setup(s => s.GetPlaylistTracksAsync(accessToken, playlistId, customOffset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        PlaylistTracksDTO result = await _playlistManager.GetTracksByPlaylist(sessionId, playlistId, customOffset);

        result.Offset.Should().Be(customOffset);
        result.NextOffset.Should().Be(customOffset + expectedDto.Limit);

        _memoryCache.TryGetValue($"playlist_offset_{sessionId}_{playlistId}", out int cachedOffset);
        cachedOffset.Should().Be(customOffset + expectedDto.Limit);
    }

    [Fact]
    public async Task GetTracksByPlaylist_ShouldUseCachedOffset_WhenOffsetIsNull()
    {
        const string sessionId = "session123";
        const string playlistId = "playlistABC";
        const string accessToken = "tokenXYZ";

        const int cachedOffset = 200;
        _memoryCache.Set($"playlist_offset_{sessionId}_{playlistId}", cachedOffset);

        PlaylistTracksDTO expectedDto = new PlaylistTracksDTO { PlaylistId = playlistId, Limit = 10 };

        _tokenServiceMock
            .Setup(t => t.GetAccessTokenAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessToken);

        _spotifyApiHelperMock
            .Setup(s => s.GetPlaylistTracksAsync(accessToken, playlistId, cachedOffset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        PlaylistTracksDTO result = await _playlistManager.GetTracksByPlaylist(sessionId, playlistId, null);

        result.Offset.Should().Be(cachedOffset);
        result.NextOffset.Should().Be(cachedOffset + expectedDto.Limit);

        _memoryCache.TryGetValue($"playlist_offset_{sessionId}_{playlistId}", out int newOffset);
        newOffset.Should().Be(cachedOffset + expectedDto.Limit);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDependenciesAreNull()
    {
        Action act1 = () => new PlaylistManager(null!, _spotifyApiHelperMock.Object, _memoryCache);
        Action act2 = () => new PlaylistManager(_tokenServiceMock.Object, null!, _memoryCache);
        Action act3 = () => new PlaylistManager(_tokenServiceMock.Object, _spotifyApiHelperMock.Object, null!);

        act1.Should().Throw<ArgumentNullException>();
        act2.Should().Throw<ArgumentNullException>();
        act3.Should().Throw<ArgumentNullException>();
    }
}
