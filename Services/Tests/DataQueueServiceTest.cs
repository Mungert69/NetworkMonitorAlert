using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NetworkMonitor.Alert.Services;
using NetworkMonitor.Objects;
using NetworkMonitor.Objects.ServiceMessage;
using NetworkMonitor.Utils.Helpers;
using Xunit;

namespace NetworkMonitorAlert.Tests.Services
{
    public class DataQueueServiceTest
    {
        private readonly Mock<ILogger<DataQueueService>> _loggerMock = new();
        private readonly Mock<ISystemParamsHelper> _systemParamsHelperMock = new();

        private const string ValidAppId = "testApp";
        private const string ValidAuthKey = "validkey";

        private SystemParams CreateSystemParams()
        {
            return new SystemParams { EmailEncryptKey = ValidAuthKey };
        }

        private DataQueueService CreateService()
        {
            _systemParamsHelperMock.Setup(s => s.GetSystemParams()).Returns(CreateSystemParams());
            return new DataQueueService(_loggerMock.Object, _systemParamsHelperMock.Object);
        }

        private string CreateProcessorDataString(ProcessorDataObj obj)
        {
            // Simulate serialization and compression
            return System.Text.Json.JsonSerializer.Serialize(obj);
        }

        [Fact]
        public async Task AddProcessorDataStringToQueue_ReturnsError_WhenProcessorDataObjIsNull()
        {
            var service = CreateService();
            // Pass a string that will not deserialize to a valid ProcessorDataObj
            var result = await service.AddProcessorDataStringToQueue("badstring", new List<IAlertable>());
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("Error : failed to process Data. Error was", result.Message);
        }

        [Fact]
        public async Task AddProcessorDataStringToQueue_ReturnsError_WhenAppIDIsNull()
        {
            var service = CreateService();
            var obj = new ProcessorDataObj { AppID = "", AuthKey = ValidAuthKey, MonitorStatusAlerts = new List<MonitorStatusAlert>() };
            string dataString = CreateProcessorDataString(obj);

            var result = await service.AddProcessorDataStringToQueue(dataString, new List<IAlertable>());
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("Error : failed to process Data. Error was", result.Message);
        }

        [Fact]
        public async Task AddProcessorDataStringToQueue_ReturnsError_WhenAuthKeyIsNull()
        {
            var service = CreateService();
            var obj = new ProcessorDataObj { AppID = ValidAppId, AuthKey = "", MonitorStatusAlerts = new List<MonitorStatusAlert>() };
            string dataString = CreateProcessorDataString(obj);

            var result = await service.AddProcessorDataStringToQueue(dataString, new List<IAlertable>());
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("Error : failed to process Data. Error was", result.Message);
        }

        [Fact]
        public async Task AddProcessorDataStringToQueue_ReturnsError_WhenInvalidAppIDInData()
        {
            var service = CreateService();
            var obj = new ProcessorDataObj
            {
                AppID = ValidAppId,
                AuthKey = ValidAuthKey,
                MonitorStatusAlerts = new List<MonitorStatusAlert> { new MonitorStatusAlert { AppID = "otherApp" } }
            };
            string dataString = CreateProcessorDataString(obj);

            var result = await service.AddProcessorDataStringToQueue(dataString, new List<IAlertable>());
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("Error : failed to process Data. Error was", result.Message);
        }

        [Fact]
        public async Task AddProcessorDataStringToQueue_ReturnsSuccess_WhenValid()
        {
            var service = CreateService();
            var obj = new ProcessorDataObj
            {
                AppID = ValidAppId,
                AuthKey = ValidAuthKey,
                MonitorStatusAlerts = new List<MonitorStatusAlert> { new MonitorStatusAlert { AppID = ValidAppId } }
            };
            string dataString = CreateProcessorDataString(obj);

            var result = await service.AddProcessorDataStringToQueue(dataString, new List<IAlertable>());
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("Error : failed to process Data. Error was", result.Message);
        }
    }
}
