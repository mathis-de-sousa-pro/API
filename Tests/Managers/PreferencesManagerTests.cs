using System.Data.Common;
using API.Managers;
using Api.Managers.InterfacesDao;
using API.Managers.InterfacesServices;
using Api.Models;
using Moq;

namespace Tests.Managers;

public class PreferencesManagerTests
{
    private static Mock<IAuditService> CreateAuditMock()
    {
        var mock = new Mock<IAuditService>();
        mock.Setup(
                a => a.LogActionAsync(
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<object?>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(
                a => a.LogAuthAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<object?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    [Fact]
    public async Task ReplaceSelectionAsync_ReplacesSelection()
    {
        var selectionDao = new Mock<IPlaylistSelectionDao>();
        var tokenDao = new Mock<ITokenDao>();
        var txRunner = new Mock<ITransactionRunner>();
        var clock = new Mock<IClockService>();

        var sessionId = "session";
        var playlistIds = new List<string> { "id1", "id2" };
        var tokenSet = new Mock<TokenSet>(
            1,
            "Spotify",
            "user",
            "refresh",
            "scope",
            DateTime.UtcNow.AddMinutes(60),
            DateTime.UtcNow,
            sessionId
        ).Object;

        tokenDao.Setup(d => d.GetBySessionAsync(sessionId)).ReturnsAsync(tokenSet);
        clock.Setup(c => c.GetUtcNow()).Returns(DateTime.UtcNow);

        var audit = CreateAuditMock();

        txRunner
            .Setup(t => t.RunAsync(
                    It.IsAny<Func<DbConnection, DbTransaction, Task>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        var manager = new PreferencesManager(
            selectionDao.Object,
            tokenDao.Object,
            txRunner.Object,
            audit.Object,
            clock.Object
        );

        await manager.ReplaceSelectionAsync(sessionId, playlistIds);

        // Pas de vérification de log
    }

    [Fact]
    public async Task AddToSelectionAsync_AddsSelection()
    {
        var selectionDao = new Mock<IPlaylistSelectionDao>();
        var tokenDao = new Mock<ITokenDao>();
        var txRunner = new Mock<ITransactionRunner>();
        var clock = new Mock<IClockService>();

        var sessionId = "session";
        var playlistIds = new List<string> { "id1", "id2" };
        var tokenSet = new Mock<TokenSet>(
            1,
            "Spotify",
            "user",
            "refresh",
            "scope",
            DateTime.UtcNow.AddMinutes(60),
            DateTime.UtcNow,
            sessionId
        ).Object;

        tokenDao.Setup(d => d.GetBySessionAsync(sessionId)).ReturnsAsync(tokenSet);
        clock.Setup(c => c.GetUtcNow()).Returns(DateTime.UtcNow);

        var audit = CreateAuditMock();

        txRunner
            .Setup(t => t.RunAsync(
                    It.IsAny<Func<DbConnection, DbTransaction, Task>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(Task.CompletedTask);

        // Adapter la signature à la version refactorée du DAO (DbConnection/DbTransaction)
        selectionDao
            .Setup(d => d.BulkInsertIfNotExistsAsync(
                    sessionId,
                    "spotify",
                    "user",
                    playlistIds,
                    It.IsAny<DateTime>(),
                    It.IsAny<DbConnection>(),
                    It.IsAny<DbTransaction>()
                )
            )
            .ReturnsAsync(2);

        var manager = new PreferencesManager(
            selectionDao.Object,
            tokenDao.Object,
            txRunner.Object,
            audit.Object,
            clock.Object
        );

        await manager.AddToSelectionAsync(sessionId, playlistIds);

        // Pas de vérification de log
    }

    [Fact]
    public async Task AddToSelectionAsync_DoesNothingWhenPlaylistIdsIsNullOrEmpty()
    {
        var selectionDao = new Mock<IPlaylistSelectionDao>();
        var tokenDao = new Mock<ITokenDao>();
        var txRunner = new Mock<ITransactionRunner>();
        var clock = new Mock<IClockService>();

        var sessionId = "session";
        var audit = CreateAuditMock();
        var manager = new PreferencesManager(
            selectionDao.Object,
            tokenDao.Object,
            txRunner.Object,
            audit.Object,
            clock.Object
        );

        await manager.AddToSelectionAsync(sessionId, null);
        await manager.AddToSelectionAsync(sessionId, new List<string>());

        // Pas de vérification de log
    }

    [Fact]
    public async Task RemoveFromSelectionAsync_DoesNothingWhenPlaylistIdsIsNullOrEmpty()
    {
        var selectionDao = new Mock<IPlaylistSelectionDao>();
        var tokenDao = new Mock<ITokenDao>();
        var txRunner = new Mock<ITransactionRunner>();
        var clock = new Mock<IClockService>();

        var sessionId = "session";
        var audit = CreateAuditMock();
        var manager = new PreferencesManager(
            selectionDao.Object,
            tokenDao.Object,
            txRunner.Object,
            audit.Object,
            clock.Object
        );

        await manager.RemoveFromSelectionAsync(sessionId, null);
        await manager.RemoveFromSelectionAsync(sessionId, new List<string>());

        // Pas de vérification de log
    }

    [Fact]
    public async Task ClearSelectionAsync_ClearsSelection()
    {
        var selectionDao = new Mock<IPlaylistSelectionDao>();
        var tokenDao = new Mock<ITokenDao>();
        var txRunner = new Mock<ITransactionRunner>();
        var clock = new Mock<IClockService>();

        var sessionId = "session";
        selectionDao.Setup(d => d.DeleteBySessionAsync(sessionId)).Returns(Task.CompletedTask);
        var audit = CreateAuditMock();

        var manager = new PreferencesManager(
            selectionDao.Object,
            tokenDao.Object,
            txRunner.Object,
            audit.Object,
            clock.Object
        );

        await manager.ClearSelectionAsync(sessionId);

        // Pas de vérification de log
    }

    [Fact]
    public async Task GetSelectionAsync_ReturnsSelectionIds()
    {
        var selectionDao = new Mock<IPlaylistSelectionDao>();
        var tokenDao = new Mock<ITokenDao>();
        var txRunner = new Mock<ITransactionRunner>();
        var clock = new Mock<IClockService>();

        var sessionId = "session";
        var ids = new List<string> { "id1", "id2" };
        selectionDao.Setup(d => d.GetIdsBySessionAsync(sessionId)).ReturnsAsync(ids);
        var audit = CreateAuditMock();

        var manager = new PreferencesManager(
            selectionDao.Object,
            tokenDao.Object,
            txRunner.Object,
            audit.Object,
            clock.Object
        );

        var result = await manager.GetSelectionAsync(sessionId);

        Assert.Equal(ids, result);
    }

    [Fact]
    public async Task ReplaceSelectionAsync_ThrowsArgumentException_WhenSessionIdIsNullOrEmpty()
    {
        var selectionDao = new Mock<IPlaylistSelectionDao>();
        var tokenDao = new Mock<ITokenDao>();
        var txRunner = new Mock<ITransactionRunner>();
        var clock = new Mock<IClockService>();

        var audit = CreateAuditMock();
        var manager = new PreferencesManager(
            selectionDao.Object,
            tokenDao.Object,
            txRunner.Object,
            audit.Object,
            clock.Object
        );

        await Assert.ThrowsAsync<ArgumentException>(() => manager.ReplaceSelectionAsync(null, new List<string>()));
        await Assert.ThrowsAsync<ArgumentException>(() => manager.ReplaceSelectionAsync("", new List<string>()));
        await Assert.ThrowsAsync<ArgumentException>(() => manager.ReplaceSelectionAsync("   ", new List<string>()));
    }
}