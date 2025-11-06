using Api.Managers.InterfacesDao;
using API.Managers.InterfacesServices;
using Api.Models;
using API.Services;
using Moq;

namespace Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="SessionService"/>.
    /// </summary>
    public class SessionServiceTests
    {
        private readonly Mock<ISessionDao> _dao = new Mock<ISessionDao>();
        private readonly Mock<IIdGenerator> _ids = new Mock<IIdGenerator>();

        [Fact]
        public async Task CreateSessionAsync_ShouldGenerateId_AndInsert()
        {
            _ids.Setup(i => i.NewSessionId()).Returns("session123");
            var now = DateTime.UtcNow;
            var svc = new SessionService(_dao.Object, _ids.Object);

            string sid = await svc.CreateSessionAsync("deviceX", now, now.AddMinutes(30));

            Assert.Equal("session123", sid);
            _dao.Verify(d => d.InsertAsync(It.Is<AppSession>(s => s.SessionId == "session123"), CancellationToken.None), Times.Once);        }

        [Fact]
        public async Task GetSessionAsync_ShouldCallDao()
        {
            var expected = new AppSession("sid", "dev", DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1));
            _dao.Setup(d => d.GetAsync("sid", CancellationToken.None)).ReturnsAsync(expected);
            var svc = new SessionService(_dao.Object, _ids.Object);

            var res = await svc.GetSessionAsync("sid");

            Assert.Equal(expected, res);
        }

        [Fact]
        public async Task DeleteAsync_ShouldCallDao()
        {
            var svc = new SessionService(_dao.Object, _ids.Object);

            await svc.DeleteAsync("sid");

            _dao.Verify(d => d.DeleteAsync("sid", CancellationToken.None), Times.Once);
        }
        
    }
}