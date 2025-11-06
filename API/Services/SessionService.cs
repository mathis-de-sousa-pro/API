#nullable enable

using System.Data.Common;
using Api.Managers.InterfacesDao;
using Api.Managers.InterfacesServices;
using API.Managers.InterfacesServices;
using Api.Models;

namespace API.Services;

/// <summary>
/// Implementation of <see cref="ISessionService"/> for managing application sessions.
/// </summary>
public class SessionService(ISessionDao sessionDao, IIdGenerator ids) : ISessionService
{
    private readonly ISessionDao _sessionDao = sessionDao ?? throw new ArgumentNullException(nameof(sessionDao));
    private readonly IIdGenerator _ids = ids ?? throw new ArgumentNullException(nameof(ids));

    /// <inheritdoc />
    public async Task<string> CreateSessionAsync(string? deviceInfo, DateTime createdAtUtc, DateTime expiresAtUtc)
    {
        string id = _ids.NewSessionId();
        AppSession session = new AppSession(id, deviceInfo ?? string.Empty, createdAtUtc, createdAtUtc, expiresAtUtc);
        await _sessionDao.InsertAsync(session);
        return id;
    }

    /// <inheritdoc />
    public Task<AppSession?> GetSessionAsync(string sessionId) => _sessionDao.GetAsync(sessionId);

    /// <inheritdoc />
    public Task DeleteAsync(string sessionId) => _sessionDao.DeleteAsync(sessionId);

    /// <inheritdoc />
    public Task DeleteAsync(string sessionId, DbConnection conn, DbTransaction tx)
        => _sessionDao.DeleteAsync(sessionId, conn, tx);
}