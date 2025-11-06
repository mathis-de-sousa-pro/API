using API.Controllers.InterfacesManagers;
using API.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>
    /// Controller for playlists
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class PlaylistController : ControllerBase
    {
        private readonly IPlaylistManager _playlistManager;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="playlistManager">There as dependency injection</param>
        public PlaylistController(IPlaylistManager playlistManager)
        {
            _playlistManager = playlistManager;
        }
        
        /// <summary>
        /// Get all tracks by a playlist
        /// </summary>
        /// <param name="playlistId">ID of the playlist</param>
        /// <param name="sessionId">Current session ID</param>
        /// <param name="offset">Position in tracks to generate 20 tracks</param>
        /// <param name="ct">Cancellation token</param>
        /// <response code="200">Returns 20 tracks from the actual offset from a playlist</response>
        /// <response code="401">Bad or expired token</response>
        /// <response code="401">Bad OAuth request</response>
        /// <response code="401">The app has exceeded its rate limits</response>
        /// <returns>The playlist ID, 20 tracks</returns>
        [HttpGet("/playlists/{playlistId}/tracks")]
        [ProducesResponseType(typeof(PlaylistTracksDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> GetPlaylistTracksAsync(
            [FromRoute(Name = "playlistId")] string playlistId, 
            [FromQuery(Name = "X-Session-Id")] string sessionId, 
            [FromQuery] int? offset,
            CancellationToken ct)
        {
            PlaylistTracksDTO result = await _playlistManager.GetTracksByPlaylist(sessionId, playlistId, offset, ct);
            return Ok(result);
        }
    }
}
