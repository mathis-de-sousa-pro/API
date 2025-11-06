using API.Controllers.InterfacesManagers;
using Api.Managers.InterfacesDao;
using API.Managers.InterfacesServices;

namespace API.Managers;

/// <summary>
/// Implements playlist preferences operations (replace/add/remove/clear/get) using DAO + transaction runner.
/// </summary>
public sealed class PreferencesManager(
    IPlaylistSelectionDao selectionDao,
    ITokenDao tokenDao,
    ITransactionRunner txRunner,
    IAuditService audit,
    IClockService clock)
    : IPreferencesManager
{
    private readonly IPlaylistSelectionDao _selectionDao =
        selectionDao ?? throw new ArgumentNullException(nameof(selectionDao));

    private readonly ITokenDao _tokenDao = tokenDao ?? throw new ArgumentNullException(nameof(tokenDao));
    private readonly ITransactionRunner _txRunner = txRunner ?? throw new ArgumentNullException(nameof(txRunner));
    private readonly IAuditService _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    private readonly IClockService _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <inheritdoc />
    public async Task ReplaceSelectionAsync(string sessionId, IReadOnlyCollection<string>? playlistIds,
        CancellationToken ct = default)
    {
        EnsureSession(sessionId);
        playlistIds ??= [];

        var tokenSet = await _tokenDao.GetBySessionAsync(sessionId)
                       ?? throw new InvalidOperationException("No TokenSet for the given session.");
        string provider = "spotify";
        string providerUserId = tokenSet.ProviderUserId ?? throw new InvalidOperationException("Missing ProviderUserId.");
        DateTime now = _clock.GetUtcNow();

        await _txRunner.RunAsync(
            async (conn, tx) =>
            {
                await _selectionDao.DeleteBySessionAsync(sessionId, conn, tx);
                if (playlistIds.Count > 0)
                {
                    await _selectionDao.BulkInsertAsync(sessionId, provider, providerUserId, playlistIds, now, conn, tx);
                }
            },
            ct
        );

        await _audit.LogActionAsync(
            sessionId,
            tokenSet.ProviderUserId,
            "preferences.replace_selection",
            "playlist.selection",
            new { count = playlistIds.Count },
            ct);
    }

    /// <inheritdoc />
    public async Task AddToSelectionAsync(string sessionId, IReadOnlyCollection<string> playlistIds,
        CancellationToken ct = default)
    {
        EnsureSession(sessionId);

        int inserted = 0;
        string? providerUserId = null;
        if (playlistIds != null && playlistIds.Count > 0)
        {
            var tokenSet = await _tokenDao.GetBySessionAsync(sessionId)
                           ?? throw new InvalidOperationException("No TokenSet for the given session.");
            string provider = "spotify";
            providerUserId = tokenSet.ProviderUserId ?? throw new InvalidOperationException("Missing ProviderUserId.");
            DateTime now = _clock.GetUtcNow();

            await _txRunner.RunAsync(
                async (conn, tx) =>
                {
                    inserted = await _selectionDao.BulkInsertIfNotExistsAsync(
                        sessionId,
                        provider,
                        providerUserId,
                        playlistIds,
                        now,
                        conn,
                        tx
                    );
                },
                ct
            );
        }

        await _audit.LogActionAsync(
            sessionId,
            providerUserId,
            "preferences.add_to_selection",
            "playlist.selection",
            new { added = inserted },
            ct);
    }

    /// <inheritdoc />
    public async Task RemoveFromSelectionAsync(string sessionId, IReadOnlyCollection<string> playlistIds,
        CancellationToken ct = default)
    {
        EnsureSession(sessionId);

        int removed = 0;
        if (playlistIds != null && playlistIds.Count > 0)
        {
            await _txRunner.RunAsync(
                async (conn, tx) => { removed = await _selectionDao.BulkDeleteByIdsAsync(sessionId, playlistIds, conn, tx); },
                ct
            );
        }

        await _audit.LogActionAsync(
            sessionId,
            null,
            "preferences.remove_from_selection",
            "playlist.selection",
            new { removed },
            ct);
    }

    /// <inheritdoc />
    public async Task ClearSelectionAsync(string sessionId, CancellationToken ct = default)
    {
        EnsureSession(sessionId);

        await _selectionDao.DeleteBySessionAsync(sessionId);
        await _audit.LogActionAsync(
            sessionId,
            null,
            "preferences.clear_selection",
            "playlist.selection",
            new { cleared = true },
            ct);
    }

    /// <inheritdoc />
    public async Task<List<string>> GetSelectionAsync(string sessionId, CancellationToken ct = default)
    {
        EnsureSession(sessionId);

        var selection = await _selectionDao.GetIdsBySessionAsync(sessionId);
        return (List<string>)selection;
    }

    private static void EnsureSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("sessionId cannot be null or empty.", nameof(sessionId));
    }
}