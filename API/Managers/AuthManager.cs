using API.Controllers.InterfacesManagers;
using API.DTO;
using API.Errors;
using Api.Managers.InterfacesDao;
using Api.Managers.InterfacesHelpers;
using Api.Managers.InterfacesServices;
using API.Managers.InterfacesServices;
using Api.Models;

namespace API.Managers;

/// <summary>
/// Handles authentication-related operations, including starting authentication flows, handling callbacks, and logging out.
/// </summary>
public class AuthManager(
    ICryptoHelper crypto,
    IUrlBuilderHelper urlBuilder,
    IPkceDao pkceDao,
    ISpotifyOAuthHelper oauth,
    ITokenDao tokenDao,
    ISessionService session,
    IDeeplinkHelper deeplink,
    IClockService clock,
    IConfigService config,
    IAccessTokenDao accessTokenDao,
    IPlaylistSelectionDao playlistSelectionDao,
    IPlaylistCacheDao playlistCacheDao,
    IUserProfileCacheDao userProfileCacheDao,
    ITokenDenyListService denylist,
    IHashService hash,
    IAuditService audit,
    ITransactionRunner txRunner)
    : IAuthManager
{
    private readonly ICryptoHelper _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
    private readonly IUrlBuilderHelper _urlBuilder = urlBuilder ?? throw new ArgumentNullException(nameof(urlBuilder));
    private readonly IPkceDao _pkceDao = pkceDao ?? throw new ArgumentNullException(nameof(pkceDao));
    private readonly ISpotifyOAuthHelper _oauth = oauth ?? throw new ArgumentNullException(nameof(oauth));
    private readonly ITokenDao _tokenDao = tokenDao ?? throw new ArgumentNullException(nameof(tokenDao));
    private readonly ISessionService _session = session ?? throw new ArgumentNullException(nameof(session));
    private readonly IDeeplinkHelper _deeplink = deeplink ?? throw new ArgumentNullException(nameof(deeplink));
    private readonly IClockService _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    private readonly IConfigService _config = config ?? throw new ArgumentNullException(nameof(config));

    private readonly IAccessTokenDao
        _accessTokenDao = accessTokenDao ?? throw new ArgumentNullException(nameof(accessTokenDao));

    private readonly IPlaylistSelectionDao _playlistSelectionDao =
        playlistSelectionDao ?? throw new ArgumentNullException(nameof(playlistSelectionDao));

    private readonly IPlaylistCacheDao _playlistCacheDao =
        playlistCacheDao ?? throw new ArgumentNullException(nameof(playlistCacheDao));

    private readonly IUserProfileCacheDao _userProfileCacheDao =
        userProfileCacheDao ?? throw new ArgumentNullException(nameof(userProfileCacheDao));

    private readonly ITokenDenyListService _denylist = denylist ?? throw new ArgumentNullException(nameof(denylist));
    private readonly IHashService _hash = hash ?? throw new ArgumentNullException(nameof(hash));
    private readonly IAuditService _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    private readonly ITransactionRunner _txRunner = txRunner ?? throw new ArgumentNullException(nameof(txRunner));

    /// <inheritdoc />
    public async Task<AuthStartResponseDto> StartAuthAsync(IList<string> scopes)
    {
        if (scopes == null || scopes.Count == 0)
            throw new ArgumentException("scopes cannot be null or empty.", nameof(scopes));

        string state = _crypto.GenerateState(32);
        _crypto.GeneratePkce(out string codeVerifier, out string codeChallenge);

        DateTime now = _clock.GetUtcNow();
        DateTime exp = now.AddMinutes(_config.GetPkceTtlMinutes());

        PkceEntry entry = new PkceEntry(state, codeVerifier, codeChallenge, exp);
        await _pkceDao.SaveAsync(entry);

        string url = _urlBuilder.BuildAuthorizeUrl(
            _config.GetSpotifyClientId(),
            _config.GetSpotifyRedirectUri(),
            scopes.ToArray(),
            state,
            codeChallenge,
            "S256"
        );

        return new AuthStartResponseDto(url, state);
    }

    /// <inheritdoc />
    public async Task<string> HandleCallbackAsync(string code, string state, string? deviceInfo)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("code cannot be null or empty.", nameof(code));
        if (string.IsNullOrWhiteSpace(state)) throw new ArgumentException("state cannot be null or empty.", nameof(state));

        PkceEntry entry = await _pkceDao.GetAsync(state) ?? throw new InvalidStateException("Unknown state.");
        DateTime now = _clock.GetUtcNow();
        if (entry.IsExpired(now))
        {
            await _pkceDao.DeleteAsync(state);
            throw new InvalidStateException("Expired state.");
        }

        TokenInfo tokens;
        try
        {
            tokens = await _oauth.ExchangeCodeForTokensAsync(
                code,
                _config.GetSpotifyRedirectUri(),
                entry.CodeVerifier
            );
        }
        catch (Exception ex)
        {
            throw new TokenExchangeFailedException("Failed to exchange code for tokens.", ex);
        }

        long tokenSetId = await _tokenDao.SaveByStateAsync(
            state,
            "spotify",
            tokens.ProviderUserId,
            tokens.RefreshToken,
            tokens.Scope,
            tokens.AccessExpiresAt
        );

        string sessionId = await _session.CreateSessionAsync(
            deviceInfo ?? string.Empty,
            now,
            now.AddMinutes(_config.GetSessionTtlMinutes())
        );
        await _tokenDao.AttachToSessionAsync(tokenSetId, sessionId);
        await _pkceDao.DeleteAsync(state);

        return _deeplink.BuildDeepLink(sessionId);
    }

    /// <inheritdoc />
    public async Task LogoutAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("sessionId cannot be null or empty.", nameof(sessionId));

        DateTime now = _clock.GetUtcNow();

        TokenSet? tokenSet = await _tokenDao.GetBySessionAsync(sessionId);

        if (!string.IsNullOrWhiteSpace(tokenSet?.RefreshToken))
            await _denylist.AddAsync(_hash.Sha256Base64(tokenSet.RefreshToken), "logout", now.AddDays(90));

        await _txRunner.RunInTransaction(async (conn, tx) =>
            {
                await _accessTokenDao.DeleteBySessionAsync(sessionId, conn, tx);
                await _playlistSelectionDao.DeleteBySessionAsync(sessionId, conn, tx);

                if (!string.IsNullOrWhiteSpace(tokenSet?.ProviderUserId))
                {
                    await _playlistCacheDao.DeleteByProviderUserAsync(tokenSet.ProviderUserId, conn, tx);
                    await _userProfileCacheDao.DeleteByProviderUserAsync(tokenSet.ProviderUserId, conn, tx);
                }

                await _playlistCacheDao.DeleteLinksBySessionAsync(sessionId, conn, tx);
                await _tokenDao.DeleteBySessionAsync(sessionId, conn, tx);
                await _session.DeleteAsync(sessionId, conn, tx);
            }
        );

        await _audit.LogAuthAsync(
            "spotify",
            "logout",
            new { reason = "purge DB + denylist" },
            sessionId,
            tokenSet?.ProviderUserId);
    }
}