using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NetworkMonitor.Alert.Services;
using NetworkMonitor.Objects;
using NetworkMonitor.Objects.Repository;
using NetworkMonitor.Objects.ServiceMessage;
using Xunit;

namespace NetworkMonitorAlert.Tests.Services;

public class MqttProcessorIngressTests
{
    private readonly Mock<IAlertMessageService> _alerts = new();
    private readonly Mock<IDataQueueService> _queue = new();
    private readonly RabbitListener _listener;

    public MqttProcessorIngressTests()
    {
        _alerts.SetupGet(a => a.MonitorAlerts).Returns(new List<IAlertable>());
        _listener = new RabbitListener(_alerts.Object, _queue.Object,
            Mock.Of<ILogger<RabbitListenerBase>>(),
            new SystemParams { ThisSystemUrl = new SystemUrl {
                RequirePublisherUserId = true, EnableMqttProcessorIngress = true
            } }, Mock.Of<IBackendMessageHmacService>());
    }

    [Fact]
    public async Task MqttStatusAlerts_UsesPayloadAuthWithoutAmqpUserId()
    {
        _queue.Setup(q => q.AddProcessorDataStringToQueue("compressed", It.IsAny<List<IAlertable>>(),
            "", false)).ReturnsAsync(new ResultObj { Success = true });

        var result = await _listener.AlertUpdateMonitorStatusAlerts("compressed", mqttIngress: true);

        Assert.True(result.Success);
        _queue.Verify(q => q.AddProcessorDataStringToQueue("compressed", It.IsAny<List<IAlertable>>(),
            "", false), Times.Once);
    }

    [Fact]
    public async Task MqttResetAlerts_StillRequiresValidAuthKey()
    {
        _alerts.Setup(a => a.IsBadAuthKey("wrong", "user1-agent")).Returns(true);

        var result = await _listener.AlertMessageResetAlerts(new AlertServiceAlertObj {
            AppID = "user1-agent", AuthKey = "wrong"
        }, mqttIngress: true);

        Assert.False(result.Success);
        _alerts.Verify(a => a.ResetMonitorAlerts(It.IsAny<List<AlertFlagObj>>()), Times.Never);
    }

    [Fact]
    public async Task MqttResetAlerts_RejectsExpiredAuthKey()
    {
        _alerts.Setup(a => a.IsBadAuthKey("expired", "user1-agent")).Returns(false);
        _alerts.Setup(a => a.IsCurrentProcessorAuthKey("user1-agent", "expired")).Returns(false);

        var result = await _listener.AlertMessageResetAlerts(new AlertServiceAlertObj {
            AppID = "user1-agent", AuthKey = "expired"
        }, mqttIngress: true);

        Assert.False(result.Success);
        Assert.Contains("not current", result.Message);
        _alerts.Verify(a => a.ResetMonitorAlerts(It.IsAny<List<AlertFlagObj>>()), Times.Never);
    }
}
