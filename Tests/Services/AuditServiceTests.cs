using API.Managers.InterfacesServices;
using API.Models;
using API.Services;
using API.Services.Masking;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Tests.Services;

public class AuditServiceTests
{
    [Fact]
    public async Task LogActionAsync_ShouldEnqueueRecord_WhenEnabled()
    {
        var writer = new Mock<IAuditWriter>();
        var clock = new Mock<IClockService>();
        var masking = new Mock<IMaskingHelper>();
        var accessor = new Mock<IHttpContextAccessor>();

        clock.Setup(c => c.GetUtcNow()).Returns(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        masking.Setup(m => m.Mask(It.IsAny<string>())).Returns<string>(s => s);
        masking.Setup(m => m.Truncate(It.IsAny<string?>(), It.IsAny<int>())).Returns<string?, int>((value, _) => value);

        var context = new DefaultHttpContext();
        context.Items["CorrelationId"] = "corr-123";
        accessor.Setup(a => a.HttpContext).Returns(context);

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Observability:Audit:Enabled"] = "true" })
            .Build();

        AuditService service = new(writer.Object, clock.Object, masking.Object, accessor.Object, config);

        await service.LogActionAsync("session", "user", "action.name", "target", new { foo = "bar" });

        writer.Verify(
            w => w.EnqueueAuditAsync(
                It.Is<AuditRecord>(r => r.CorrelationId == "corr-123" && r.Action == "action.name" && r.Target == "target"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LogActionAsync_ShouldNotEnqueue_WhenDisabled()
    {
        var writer = new Mock<IAuditWriter>();
        var clock = new Mock<IClockService>();
        var masking = new Mock<IMaskingHelper>();
        var accessor = new Mock<IHttpContextAccessor>();

        clock.Setup(c => c.GetUtcNow()).Returns(DateTime.UtcNow);
        masking.Setup(m => m.Mask(It.IsAny<string>())).Returns<string>(s => s);

        accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Observability:Audit:Enabled"] = "false" })
            .Build();

        AuditService service = new(writer.Object, clock.Object, masking.Object, accessor.Object, config);

        await service.LogActionAsync("session", "user", "action.name", null, null);

        writer.Verify(
            w => w.EnqueueAuditAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
