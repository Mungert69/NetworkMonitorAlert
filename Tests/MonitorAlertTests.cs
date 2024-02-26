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

namespace NetworkMonitor.Tests
{
    public class MonitorAlertTests
    {
        private readonly Mock<ILogger<AlertMessageService>> _loggerMock;
        private readonly Mock<IRabbitRepo> _rabbitRepoMock;
        private readonly Mock<EmailProcessor> _emailProcessorMock;
        private readonly Mock<IProcessorState> _processorStateMock;
        private readonly Mock<NetConnectCollection> _netConnectCollectionMock;


        public MonitorAlertTests()
        {
            _loggerMock = new Mock<ILogger<AlertMessageService>>();
            _rabbitRepoMock = new Mock<IRabbitRepo>();
            _emailProcessorMock = new Mock<EmailProcessor>();
            _processorStateMock = new Mock<IProcessorState>();
            _netConnectCollectionMock = new Mock<NetConnectCollection>();
        }

        private List<ProcessorObj> GetProcesorList() { 
            var processorList=new List<ProcessorObj>();
            processorList.Add(new ProcessorObj() { AppID="test"});
            return processorList;
        }
        private List<IAlertable> GetAlerts() { 
            var alerts=new List<IAlertable>();
            alerts.Add(new MonitorStatusAlert() { ID=1});
            return alerts;
        }
       


        [Fact]
        public async Task CheckHost_ReturnsSuccessWhenPredictionsArePositive()
        {
            //_rabbitRepoMock.Setup(repo => repo.PublishAsync<AlertServiceInitObj>("alertServiceReady", It.IsAny<AlertServiceInitObj>())).ReturnsAsync();
            _processorStateMock.Setup(p => p.EnabledProcessorList)
                                              .Returns(new List<ProcessorObj>());
            //_emailProcessorMock.Setup(repo => repo.PublishAsync<AlertServiceInitObj>("alertServiceReady", It.IsAny<AlertServiceInitObj>())).ReturnsAsync();

            var alertProcessor = new AlertProcessor(_loggerMock.Object, _rabbitRepoMock.Object, _emailProcessorMock.Object, _processorStateMock.Object, _netConnectCollectionMock.Object);
            // Act
            alertProcessor.MonitorAlertProcess.Alerts = GetAlerts();

            var result = await alertProcessor.MonitorAlert();

            // Assert
            Assert.True(result.Success, " Call to MonitorAlert did not compete with success.");

        }

    }

}
