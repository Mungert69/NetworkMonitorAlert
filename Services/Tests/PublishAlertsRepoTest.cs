using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NetworkMonitor.Objects;
using NetworkMonitor.Objects.Repository;
using Xunit;

namespace NetworkMonitorAlert.Tests.Services
{
    public class PublishAlertsRepoTests
    {
        private readonly Mock<ILogger> _loggerMock = new();
        private readonly Mock<IRabbitRepo> _rabbitRepoMock = new();

        private List<IAlertable> CreateAlertableList(string appId, int count = 2)
        {
            var list = new List<IAlertable>();
            for (int i = 0; i < count; i++)
            {
                list.Add(new MonitorStatusAlert { ID = i + 1, AppID = appId });
            }
            return list;
        }

        private List<ProcessorObj> CreateProcessorList(string appId)
        {
            return new List<ProcessorObj> { new ProcessorObj { AppID = appId } };
        }

        [Fact]
        public async Task ProcessorAlertSent_Publishes_WhenAlertsExist()
        {
            var alerts = CreateAlertableList("app1");
            var processors = CreateProcessorList("app1");

            _rabbitRepoMock.Setup(r => r.PublishAsync<List<int>>(It.IsAny<string>(), It.IsAny<List<int>>(), ""))
                .Returns(Task.CompletedTask);

            await PublishAlertsRepo.ProcessorAlertSent(_loggerMock.Object, _rabbitRepoMock.Object, alerts, processors);

            _rabbitRepoMock.Verify(r => r.PublishAsync<List<int>>("processorAlertSentapp1", It.Is<List<int>>(ids => ids.Count == 2), ""), Times.Once);
        }

        [Fact]
        public async Task ProcessorAlertSent_DoesNotPublish_WhenNoAlerts()
        {
            var alerts = new List<IAlertable>();
            var processors = CreateProcessorList("app1");

            await PublishAlertsRepo.ProcessorAlertSent(_loggerMock.Object, _rabbitRepoMock.Object, alerts, processors);

            _rabbitRepoMock.Verify(r => r.PublishAsync<List<int>>(It.IsAny<string>(), It.IsAny<List<int>>(), ""), Times.Never);
        }

        [Fact]
        public async Task ProcessorAlertFlag_PublishesAndSetsAlertFlag()
        {
            var alerts = CreateAlertableList("app2");
            var processors = CreateProcessorList("app2");

            _rabbitRepoMock.Setup(r => r.PublishAsync<List<int>>(It.IsAny<string>(), It.IsAny<List<int>>(), ""))
                .Returns(Task.CompletedTask);

            await PublishAlertsRepo.ProcessorAlertFlag(_loggerMock.Object, _rabbitRepoMock.Object, alerts, processors);

            _rabbitRepoMock.Verify(r => r.PublishAsync<List<int>>("processorAlertFlagapp2", It.Is<List<int>>(ids => ids.Count == 2), ""), Times.Once);
            Assert.All(alerts, a => Assert.True(a.AlertFlag));
        }

        [Fact]
        public async Task ProcessorResetAlerts_PublishesForEachKey()
        {
            var monitorIPDic = new Dictionary<string, List<int>>
            {
                { "app3", new List<int> { 1, 2 } },
                { "app4", new List<int> { 3 } }
            };

            _rabbitRepoMock.Setup(r => r.PublishAsync<List<int>>(It.IsAny<string>(), It.IsAny<List<int>>(), ""))
                .Returns(Task.CompletedTask);

            await PublishAlertsRepo.ProcessorResetAlerts(_loggerMock.Object, _rabbitRepoMock.Object, monitorIPDic);

            _rabbitRepoMock.Verify(r => r.PublishAsync<List<int>>("processorResetAlertsapp3", It.Is<List<int>>(ids => ids.Count == 2), ""), Times.Once);
            _rabbitRepoMock.Verify(r => r.PublishAsync<List<int>>("processorResetAlertsapp4", It.Is<List<int>>(ids => ids.Count == 1), ""), Times.Once);
        }

        [Fact]
        public async Task PredictAlertSent_Publishes_WhenAlertsExist()
        {
            var alerts = CreateAlertableList("app5");
            _rabbitRepoMock.Setup(r => r.PublishAsync<List<int>>(It.IsAny<string>(), It.IsAny<List<int>>(), ""))
                .Returns(Task.CompletedTask);

            await PublishAlertsRepo.PredictAlertSent(_loggerMock.Object, _rabbitRepoMock.Object, alerts);

            _rabbitRepoMock.Verify(r => r.PublishAsync<List<int>>("predictAlertSent", It.Is<List<int>>(ids => ids.Count == 2), ""), Times.Once);
        }

        [Fact]
        public async Task PredictAlertSent_DoesNotPublish_WhenNoAlerts()
        {
            var alerts = new List<IAlertable>();

            await PublishAlertsRepo.PredictAlertSent(_loggerMock.Object, _rabbitRepoMock.Object, alerts);

            _rabbitRepoMock.Verify(r => r.PublishAsync<List<int>>(It.IsAny<string>(), It.IsAny<List<int>>(), ""), Times.Never);
        }

        [Fact]
        public async Task PredictAlertFlag_PublishesAndSetsAlertFlag()
        {
            var alerts = CreateAlertableList("app6");
            _rabbitRepoMock.Setup(r => r.PublishAsync<List<int>>(It.IsAny<string>(), It.IsAny<List<int>>(), ""))
                .Returns(Task.CompletedTask);

            await PublishAlertsRepo.PredictAlertFlag(_loggerMock.Object, _rabbitRepoMock.Object, alerts);

            _rabbitRepoMock.Verify(r => r.PublishAsync<List<int>>("predictAlertFlag", It.Is<List<int>>(ids => ids.Count == 2), ""), Times.Once);
            Assert.All(alerts, a => Assert.True(a.AlertFlag));
        }

        [Fact]
        public async Task PredictResetAlerts_Publishes()
        {
            var ids = new List<int> { 1, 2, 3 };
            _rabbitRepoMock.Setup(r => r.PublishAsync<List<int>>(It.IsAny<string>(), It.IsAny<List<int>>(), ""))
                .Returns(Task.CompletedTask);

            await PublishAlertsRepo.PredictResetAlerts(_loggerMock.Object, _rabbitRepoMock.Object, ids);

            _rabbitRepoMock.Verify(r => r.PublishAsync<List<int>>("predictResetAlerts", It.Is<List<int>>(l => l.Count == 3), ""), Times.Once);
        }

        [Fact]
        public async Task ProcessorAlertSent_HandlesException_AndLogsCritical()
        {
            var alerts = CreateAlertableList("app7");
            var processors = CreateProcessorList("app7");

            _rabbitRepoMock.Setup(r => r.PublishAsync<List<int>>(It.IsAny<string>(), It.IsAny<List<int>>(), ""))
                .ThrowsAsync(new Exception("Test exception"));

            await PublishAlertsRepo.ProcessorAlertSent(_loggerMock.Object, _rabbitRepoMock.Object, alerts, processors);

            _loggerMock.Verify(l => l.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() != null && v.ToString()!.Contains("Unable to send processorAlertSent message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task ProcessorResetAlerts_HandlesException_AndLogsError()
        {
            var monitorIPDic = new Dictionary<string, List<int>>
            {
                { "app8", new List<int> { 1, 2 } }
            };

            _rabbitRepoMock.Setup(r => r.PublishAsync<List<int>>(It.IsAny<string>(), It.IsAny<List<int>>(), ""))
                .ThrowsAsync(new Exception("Test exception"));

            await PublishAlertsRepo.ProcessorResetAlerts(_loggerMock.Object, _rabbitRepoMock.Object, monitorIPDic);

            _loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() != null && v.ToString()!.Contains("failed to publish ProcessResetAlerts")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task PredictAlertSent_HandlesException_AndLogsCritical()
        {
            var alerts = CreateAlertableList("app9");
            _rabbitRepoMock.Setup(r => r.PublishAsync<List<int>>(It.IsAny<string>(), It.IsAny<List<int>>(), ""))
                .ThrowsAsync(new Exception("Test exception"));

            await PublishAlertsRepo.PredictAlertSent(_loggerMock.Object, _rabbitRepoMock.Object, alerts);

            _loggerMock.Verify(l => l.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() != null && v.ToString()!.Contains("Unable to send predictAlertSent message")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task PredictResetAlerts_HandlesException_AndLogsError()
        {
            var ids = new List<int> { 1, 2, 3 };
            _rabbitRepoMock.Setup(r => r.PublishAsync<List<int>>(It.IsAny<string>(), It.IsAny<List<int>>(), ""))
                .ThrowsAsync(new Exception("Test exception"));

            await PublishAlertsRepo.PredictResetAlerts(_loggerMock.Object, _rabbitRepoMock.Object, ids);

            _loggerMock.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() != null && v.ToString()!.Contains("failed to publish PredictResetAlerts")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }
    }
}
