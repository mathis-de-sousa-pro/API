using System.Net;
using System.Text;
using System.Text.Json;
using API.DTO;
using API.Errors.Exceptions;
using API.Helpers;
using API.Managers.InterfacesServices;
using Moq;
using Moq.Protected;

namespace Tests.Helpers;

public class SpotifyApiHelperTests
{
    private readonly Mock<IConfigService> _config = new Mock<IConfigService>();
    private readonly SpotifyApiHelper _helper;

    public SpotifyApiHelperTests()
    {
        Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
        HttpClient httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.spotify.com/v1/")
        };
        _config.Setup(c => c.GetSpotifyPlaylistsPageSize()).Returns(20);
        _helper = new SpotifyApiHelper(httpClient, _config.Object);
    }

    [Fact]
    public async Task GetPlaylistsAsync_Throws_OnEmptyAccessToken()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _helper.GetPlaylistsAsync("", null));
    }

    [Fact]
    public async Task GetPlaylistsAsync_ReturnsPlaylists_OnValidResponse()
    {
        SpotifyPlaylistsResponse responseObj = new SpotifyPlaylistsResponse
        {
            Items =
            [
                new SpotifyPlaylistItem
                {
                    Id = "id1",
                    Name = "Playlist 1",
                    Images = new[] { new SpotifyImage { Url = "img1" } },
                    Owner = new SpotifyOwner { DisplayName = "owner1" },
                    Tracks = new SpotifyTracksInfo
                    {
                        Total = 10
                    }
                }
            ],
            Next = "next-token"
        };
        var json = JsonSerializer.Serialize(responseObj);
        Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json)
                }
            );
        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.spotify.com/v1/")
        };
        var helper = new SpotifyApiHelper(client, _config.Object);
        var result = await helper.GetPlaylistsAsync("token", null);
        Assert.Single(result.Items);
        Assert.Equal("id1", result.Items[0].PlaylistId);
        Assert.Equal("next-token", result.NextPageToken);
    }

    [Fact]
    public async Task GetPlaylistsAsync_Throws_OnInvalidJson()
    {
        Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(
                new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("not a json")
                }
            );
        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.spotify.com/v1/")
        };
        var helper = new SpotifyApiHelper(client, _config.Object);
        await Assert.ThrowsAsync<JsonException>(() => helper.GetPlaylistsAsync("token", null));
    }

    [Fact]
    public async Task GetPlaylistTracks_ValidRequest_ReturnsPlaylistTracksDTO()
    {
        // Arrange
        const string accessToken = "valid_token";
        string playlistId = "playlist123";
        var offset = 0;

        var spotifyResponse = new PlaylistTracksResponse
        {
            Limit = 20,
            Offset = 0,
            Items =
            [
                new SpotifyTrackItem
                {
                    Track = new SpotifyTrack
                    {
                        Id = "track1",
                        Name = "Test Song",
                        Artists =
                        [
                            new ArtistDTO { Id = "artist1", Name = "Test Artist" }
                        ],
                        Album = new AlbumDTO
                        {
                            Id = "album1",
                            Images =
                            [
                                new SpotifyImage { Url = "https://example.com/image.jpg" }
                            ]
                        }
                    }
                }
            ]
        };

        var json = JsonSerializer.Serialize(spotifyResponse);
        Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.spotify.com/v1/")
        };
        var helper = new SpotifyApiHelper(client, _config.Object);

        // Act
        PlaylistTracksDTO result = await helper.GetPlaylistTracksAsync(accessToken, playlistId, offset);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(playlistId, result.PlaylistId);
        Assert.Equal(20, result.Limit);
        Assert.Equal(0, result.Offset);
        Assert.Single(result.Tracks);
        Assert.Equal("track1", result.Tracks[0].Id);
        Assert.Equal("Test Song", result.Tracks[0].Name);
        Assert.Single(result.Tracks[0].Artists);
        Assert.Equal("artist1", result.Tracks[0].Artists[0].Id);
        Assert.NotNull(result.Tracks[0].Album);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPlaylistTracks_InvalidAccessToken_ThrowsArgumentException(string accessToken)
    {
        // Arrange
        string playlistId = "playlist123";

        // Act & Assert
        ArgumentException exception =
            await Assert.ThrowsAsync<ArgumentException>(() => _helper.GetPlaylistTracksAsync(accessToken, playlistId, 0)
            );
        Assert.Equal("accessToken", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPlaylistTracks_InvalidPlaylistId_ThrowsArgumentException(string playlistId)
    {
        // Arrange
        const string accessToken = "valid_token";

        // Act & Assert
        ArgumentException exception =
            await Assert.ThrowsAsync<ArgumentException>(() => _helper.GetPlaylistTracksAsync(accessToken, playlistId, 0)
            );
        Assert.Equal("playlistId", exception.ParamName);
    }

    [Fact]
    public async Task GetPlaylistTracks_HttpRequestFails_ThrowsHttpRequestException()
    {
        // Arrange
        const string accessToken = "valid_token";
        string playlistId = "playlist123";

        Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized
            });

        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.spotify.com/v1/")
        };
        var helper = new SpotifyApiHelper(client, _config.Object);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => helper.GetPlaylistTracksAsync(accessToken, playlistId, 0)
        );
    }

    [Fact]
    public async Task GetPlaylistTracks_NullResponse_ThrowsInvalidOperationException()
    {
        // Arrange
        const string accessToken = "valid_token";
        string playlistId = "playlist123";

        Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("null")
            });

        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.spotify.com/v1/")
        };
        var helper = new SpotifyApiHelper(client, _config.Object);

        // Act & Assert
        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                helper.GetPlaylistTracksAsync(accessToken, playlistId, 0)
            );
        Assert.Contains("Failed to deserialize", exception.Message);
    }

    [Fact]
    public async Task GetPlaylistTracks_TracksWithNullFields_HandlesGracefully()
    {
        // Arrange
        const string accessToken = "valid_token";
        string playlistId = "playlist123";

        var spotifyResponse = new PlaylistTracksResponse
        {
            Limit = 20,
            Offset = 0,
            Items =
            [
                new SpotifyTrackItem
                {
                    Track = new SpotifyTrack
                    {
                        Id = null,
                        Name = null,
                        Artists = null,
                        Album = new AlbumDTO // Album DOIT être présent
                        {
                            Id = null,
                            Images = null
                        }
                    }
                }
            ]
        };

        var json = JsonSerializer.Serialize(spotifyResponse);
        Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.spotify.com/v1/")
        };
        var helper = new SpotifyApiHelper(client, _config.Object);

        // Act
        PlaylistTracksDTO result = await helper.GetPlaylistTracksAsync(accessToken, playlistId, 0);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Tracks);
        Assert.Equal(string.Empty, result.Tracks[0].Id);
        Assert.Equal(string.Empty, result.Tracks[0].Name);
        Assert.Empty(result.Tracks[0].Artists);
        Assert.NotNull(result.Tracks[0].Album);
        Assert.Empty(result.Tracks[0].Album.Images);
    }

    [Fact]
    public async Task GetPlaylistTracks_ItemsWithNullTrack_FiltersOut()
    {
        // Arrange
        const string accessToken = "valid_token";
        string playlistId = "playlist123";

        var spotifyResponse = new PlaylistTracksResponse
        {
            Limit = 20,
            Offset = 0,
            Items =
            [
                new SpotifyTrackItem { Track = null },
                new SpotifyTrackItem
                {
                    Track = new SpotifyTrack
                    {
                        Id = "track1",
                        Name = "Valid Track",
                        Artists = new List<ArtistDTO>(),
                        Album = new AlbumDTO // Album ne doit PAS être null
                        {
                            Id = "album1",
                            Images = new List<SpotifyImage>()
                        }
                    }
                }
            ]
        };

        var json = JsonSerializer.Serialize(spotifyResponse);
        Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.spotify.com/v1/")
        };
        var helper = new SpotifyApiHelper(client, _config.Object);

        // Act
        PlaylistTracksDTO result = await helper.GetPlaylistTracksAsync(accessToken, playlistId, 0);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Tracks);
        Assert.Equal("track1", result.Tracks[0].Id);
        Assert.NotNull(result.Tracks[0].Album); // L'album doit être présent
        Assert.Equal("album1", result.Tracks[0].Album.Id);
    }

    [Fact]
    public async Task GetPlaylistTracks_CancellationTokenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        const string accessToken = "valid_token";
        string playlistId = "playlist123";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new TaskCanceledException());

        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.spotify.com/v1/")
        };
        var helper = new SpotifyApiHelper(client, _config.Object);

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            helper.GetPlaylistTracksAsync(accessToken, playlistId, 0, cts.Token)
        );
    }

    [Fact]
    public async Task GetPlaylistTracks_CorrectUrlAndHeaders_AreSent()
    {
        // Arrange
        var accessToken = "test_bearer_token";
        var playlistId = "playlist456";
        var offset = 20;

        var spotifyResponse = new PlaylistTracksResponse
        {
            Limit = 20,
            Offset = 20,
            Items = []
        };

        HttpRequestMessage capturedRequest = null;
        Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(spotifyResponse),
                    Encoding.UTF8,
                    "application/json"
                )
            });

        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.spotify.com/v1/")
        };
        var helper = new SpotifyApiHelper(client, _config.Object);

        // Act
        await helper.GetPlaylistTracksAsync(accessToken, playlistId, offset);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.Contains($"playlists/{playlistId}/tracks", capturedRequest.RequestUri.ToString());
        Assert.Contains("limit=20", capturedRequest.RequestUri.ToString());
        Assert.Contains("offset=20", capturedRequest.RequestUri.ToString());
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization.Scheme);
        Assert.Equal(accessToken, capturedRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task GetSavedTracksTotalAsync_ParsesTotal()
    {
        Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"total\": 42}")
            });

        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.spotify.com/v1/")
        };

        var helper = new SpotifyApiHelper(client, _config.Object);

        int total = await helper.GetSavedTracksTotalAsync("token");

        Assert.Equal(42, total);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    public async Task GetSavedTracksTotalAsync_Handles429Or5xx(int statusCode)
    {
        Mock<HttpMessageHandler> handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = (HttpStatusCode)statusCode
            });

        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://api.spotify.com/v1/")
        };

        var helper = new SpotifyApiHelper(client, _config.Object);

        await Assert.ThrowsAsync<RecoverableSpotifyApiException>(() => helper.GetSavedTracksTotalAsync("token"));
    }
}