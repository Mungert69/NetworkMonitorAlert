using Microsoft.Extensions.Logging;
using Moq;
using NetworkMonitor.Alert.Services;
using NetworkMonitor.Objects;
using NetworkMonitor.Objects.Repository;
using NetworkMonitor.Connection;
using NetworkMonitor.Objects.ServiceMessage;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;
using Xunit;

namespace NetworkMonitor.Alert.Tests
{
    public class MonitorAlertTests
    {
        private readonly Mock<ILogger<AlertMessageService>> _loggerMock;
        private readonly Mock<IRabbitRepo> _rabbitRepoMock;
        private readonly Mock<IEmailProcessor> _emailProcessorMock;
        private readonly Mock<IProcessorState> _processorStateMock;
        private readonly Mock<INetConnectCollection> _netConnectCollectionMock;


        public MonitorAlertTests()
        {
            _loggerMock = new Mock<ILogger<AlertMessageService>>();
            _rabbitRepoMock = new Mock<IRabbitRepo>();
            _emailProcessorMock = new Mock<IEmailProcessor>();
            _processorStateMock = new Mock<IProcessorState>();
            _netConnectCollectionMock = new Mock<INetConnectCollection>();
        }




        [Fact]
        public async Task MonitorAlert_TestSendSuccess()
        {
            //_rabbitRepoMock.Setup(repo => repo.PublishAsync<AlertServiceInitObj>("alertServiceReady", It.IsAny<AlertServiceInitObj>())).ReturnsAsync();
            _processorStateMock.Setup(p => p.EnabledProcessorList)
                                              .Returns(new List<ProcessorObj>());
            _emailProcessorMock.Setup(p => p.SendAlert(It.IsAny<AlertMessage>()))
                                             .ReturnsAsync(new ResultObj() { Success = true });
            _emailProcessorMock.Setup(p => p.VerifyEmail(It.IsAny<UserInfo>(), It.IsAny<IAlertable>())).Returns(true);
            _emailProcessorMock.Setup(p => p.VerifyEmail(It.Is<UserInfo>(u => u.UserID == "default"), It.Is<IAlertable>(a => a.ID==4))).Returns(false);

            var alertProcessor = new AlertProcessor(_loggerMock.Object, _rabbitRepoMock.Object, _emailProcessorMock.Object, _processorStateMock.Object, _netConnectCollectionMock.Object, AlertTestData.GetAlertParams(), AlertTestData.GetUserInfos());
            // Act
            alertProcessor.MonitorAlertProcess.Alerts = AlertTestData.GetAlerts();

            var result = await alertProcessor.MonitorAlert();

            // Assert
            Assert.True(result.Success, " Call to MonitorAlert did not compete with success.");
            Assert.True(!alertProcessor.MonitorAlertProcess.Alerts[0].AlertFlag, " Alert has been flagged for first line of data it.");
            Assert.True(!alertProcessor.MonitorAlertProcess.Alerts[0].AlertSent, " Alert has been sent for first line of data");

            Assert.True(alertProcessor.MonitorAlertProcess.Alerts[1].AlertFlag, " Alert has not been flagged for second line of data");
            Assert.True(alertProcessor.MonitorAlertProcess.Alerts[1].AlertSent, " Alert has not been sent for second line of data");

            Assert.True(!alertProcessor.MonitorAlertProcess.Alerts[2].AlertFlag, " Alert has been flagged but user does not exist.");
            Assert.True(!alertProcessor.MonitorAlertProcess.Alerts[2].AlertSent, " Alert has been sent but user does not exist");

            Assert.True(alertProcessor.MonitorAlertProcess.Alerts[3].AlertFlag, " Alert Flag has been reset but user has bad email");
            Assert.True(alertProcessor.MonitorAlertProcess.Alerts[3].AlertSent, " Alert Sent has not been set for a user that has bad email");

            Assert.True(alertProcessor.MonitorAlertProcess.UpdateAlertSentList.Count() == 2, " There is not only two sent alerts in the UpdateSentAlertList ");
            int count = (int)result.Data!;
            Assert.True(count == 1, " First call of MonitorAlert has not sent only one email");

            result = await alertProcessor.MonitorAlert();
            count = (int)result.Data!;
            // Assert
            Assert.True(count == 0, " Second call of MonitorAlert has sent a second email for same alert.");

        }

    }

}
