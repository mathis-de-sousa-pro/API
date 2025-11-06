using API.DTO;

namespace API.Managers.InterfacesHelpers
{
    /// <summary>
    /// Thin HTTP wrapper over Spotify Web API for data retrieval (no token logic).
    /// </summary>
    public interface ISpotifyApiHelper
    {
        /// <summary>
        /// Calls GET /v1/me/playlists (paginated) and maps the response to <see cref="PlaylistPageDto"/>.
        /// </summary>
        /// <param name="accessToken">Bearer access token.</param>
        /// <param name="pageToken">Opaque page token (maybe the Spotify "next" URL or an offset token).</param>
        /// <param name="ct">Cancellation token.</param>
        Task<PlaylistPageDto> GetPlaylistsAsync(string accessToken, string? pageToken, CancellationToken ct = default);

        /// <summary>
        /// Retrieves the list of tracks in a specific playlist from Spotify.
        /// </summary>
        /// <param name="accessToken">Valid Spotify access token.</param>
        /// <param name="playlistId">Spotify playlist ID.</param>
        /// <param name="offset">Index of the current track</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A DTO containing the playlist’s tracks.</returns>
        public Task<PlaylistTracksDTO> GetPlaylistTracks(string accessToken, string playlistId, int? offset,
            CancellationToken ct = default);

        /// <summary>
        /// Retrieves the total number of tracks saved in the current user's library.
        /// </summary>
        /// <param name="accessToken">Bearer access token.</param>
        /// <param name="ct">Cancellation token.</param>
        Task<int> GetSavedTracksTotalAsync(string accessToken, CancellationToken ct = default);
    }
}