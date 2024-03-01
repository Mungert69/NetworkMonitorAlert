using Microsoft.Extensions.Logging;
using Moq;
using NetworkMonitor.Alert.Services;
using NetworkMonitor.Objects;
using NetworkMonitor.Objects.Repository;
using NetworkMonitor.Connection;
using NetworkMonitor.Objects.ServiceMessage;
using NetworkMonitor.Utils.Helpers;
using NetworkMonitor.Utils;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;
using Xunit;

namespace NetworkMonitor.Alert.Tests
{
    public class MonitorAlertTests
    {
        private readonly Mock<ILogger<AlertMessageService>> _loggerAlertProcessorMock;
        private readonly Mock<ILogger<DataQueueService>> _loggerDataQueueMock;
        private readonly Mock<IRabbitRepo> _rabbitRepoMock;
        private readonly Mock<IEmailProcessor> _emailProcessorMock;
        private readonly Mock<IProcessorState> _processorStateMock;
        private readonly Mock<INetConnectCollection> _netConnectCollectionMock;
        private readonly Mock<ISystemParamsHelper> _systemParamsHelperMock;
        private readonly Mock<SystemParams> _systemParamsMock;


        public MonitorAlertTests()
        {
            _loggerAlertProcessorMock = new Mock<ILogger<AlertMessageService>>();
            _loggerDataQueueMock = new Mock<ILogger<DataQueueService>>();
            _rabbitRepoMock = new Mock<IRabbitRepo>();
            _emailProcessorMock = new Mock<IEmailProcessor>();
            _processorStateMock = new Mock<IProcessorState>();
            _netConnectCollectionMock = new Mock<INetConnectCollection>();
            _systemParamsHelperMock = new Mock<ISystemParamsHelper>();
            _systemParamsMock = new Mock<SystemParams>();
        }



        [Fact]
        public async Task MonitorAlert_TestSuccess()
        {
            //_rabbitRepoMock.Setup(repo => repo.PublishAsync<AlertServiceInitObj>("alertServiceReady", It.IsAny<AlertServiceInitObj>())).ReturnsAsync();
            _processorStateMock.Setup(p => p.EnabledProcessorList)
                                              .Returns(new List<ProcessorObj>());
            _emailProcessorMock.Setup(p => p.SendAlert(It.IsAny<AlertMessage>()))
                                             .ReturnsAsync(new ResultObj() { Success = true });
            _emailProcessorMock.Setup(p => p.VerifyEmail(It.IsAny<UserInfo>(), It.IsAny<IAlertable>())).Returns(true);
            _emailProcessorMock.Setup(p => p.VerifyEmail(It.Is<UserInfo>(u => u.UserID == "default"), It.Is<IAlertable>(a => a.ID == 4))).Returns(false);

            var alertProcessor = new AlertProcessor(_loggerAlertProcessorMock.Object, _rabbitRepoMock.Object, _emailProcessorMock.Object, _processorStateMock.Object, _netConnectCollectionMock.Object, AlertTestData.GetAlertParams(), AlertTestData.GetUserInfos());
            // Act
            alertProcessor.MonitorAlertProcess.Alerts = AlertTestData.GetMonitorAlerts();

            var result = await alertProcessor.MonitorAlert();

            // Assert
            Assert.True(result.Success, $" Call to MonitorAlert did not compete with success. {result.Message}");
            Assert.True(!alertProcessor.MonitorAlertProcess.Alerts[0].AlertFlag, " Alert has been flagged for first line of data it.");
            Assert.True(!alertProcessor.MonitorAlertProcess.Alerts[0].AlertSent, " Alert has been sent for first line of data");

            Assert.True(alertProcessor.MonitorAlertProcess.Alerts[1].AlertFlag, " Alert has not been flagged for second line of data");
            Assert.True(alertProcessor.MonitorAlertProcess.Alerts[1].AlertSent, " Alert has not been sent for second line of data");

            Assert.True(!alertProcessor.MonitorAlertProcess.Alerts[2].AlertFlag, " Alert has been flagged but user does not exist.");
            Assert.True(!alertProcessor.MonitorAlertProcess.Alerts[2].AlertSent, " Alert has been sent but user does not exist");

            Assert.True(alertProcessor.MonitorAlertProcess.Alerts[3].AlertFlag, " Alert Flag has been reset but user has bad email");
            Assert.True(alertProcessor.MonitorAlertProcess.Alerts[3].AlertSent, " Alert Sent has not been set for a user that has bad email");

            Assert.True(alertProcessor.MonitorAlertProcess.UpdateAlertSentList.Count() == 4, " There is not only two sent alerts in the UpdateSentAlertList ");
            int count = (int)result.Data!;
            Assert.True(count == 1, " First call of MonitorAlert has not sent only one email");

            result = await alertProcessor.MonitorAlert();
            Assert.True(result.Success, $" 2nd Call to MonitorAlert did not compete with success. {result.Message}");

            count = (int)result.Data!;
            // Assert
            Assert.True(count == 0, " Second call of MonitorAlert has sent a second email for same alert.");

        }
        [Fact]
        public async Task PredictAlert_TestSuccess()
        {
            //_rabbitRepoMock.Setup(repo => repo.PublishAsync<AlertServiceInitObj>("alertServiceReady", It.IsAny<AlertServiceInitObj>())).ReturnsAsync();
            _processorStateMock.Setup(p => p.EnabledProcessorList)
                                              .Returns(new List<ProcessorObj>());
            _emailProcessorMock.Setup(p => p.SendAlert(It.IsAny<AlertMessage>()))
                                             .ReturnsAsync(new ResultObj() { Success = true });
            _emailProcessorMock.Setup(p => p.VerifyEmail(It.IsAny<UserInfo>(), It.IsAny<IAlertable>())).Returns(true);
            _emailProcessorMock.Setup(p => p.VerifyEmail(It.Is<UserInfo>(u => u.UserID == "default"), It.Is<IAlertable>(a => a.ID == 4))).Returns(false);

            var alertProcessor = new AlertProcessor(_loggerAlertProcessorMock.Object, _rabbitRepoMock.Object, _emailProcessorMock.Object, _processorStateMock.Object, _netConnectCollectionMock.Object, AlertTestData.GetAlertParams(), AlertTestData.GetUserInfos());
            // Act
            alertProcessor.PredictAlertProcess.Alerts = AlertTestData.GetPredictAlerts();

            var result = await alertProcessor.PredictAlert();

            // Assert
            Assert.True(result.Success, $" Call to PredictAlert did not compete with success. {result.Message}");
            Assert.True(!alertProcessor.PredictAlertProcess.Alerts[0].AlertFlag, " Alert has been flagged for first line of data it.");
            Assert.True(!alertProcessor.PredictAlertProcess.Alerts[0].AlertSent, " Alert has been sent for first line of data");

            Assert.True(alertProcessor.PredictAlertProcess.Alerts[1].AlertFlag, " Alert has not been flagged for second line of data");
            Assert.True(alertProcessor.PredictAlertProcess.Alerts[1].AlertSent, " Alert has not been sent for second line of data");

            Assert.True(!alertProcessor.PredictAlertProcess.Alerts[2].AlertFlag, " Alert has been flagged but user does not exist.");
            Assert.True(!alertProcessor.PredictAlertProcess.Alerts[2].AlertSent, " Alert has been sent but user does not exist");

            Assert.True(alertProcessor.PredictAlertProcess.Alerts[3].AlertFlag, " Alert Flag has been reset but user has bad email");
            Assert.True(alertProcessor.PredictAlertProcess.Alerts[3].AlertSent, " Alert Sent has not been set for a user that has bad email");

            Assert.True(alertProcessor.PredictAlertProcess.UpdateAlertSentList.Count() == 3, " There is not only two sent alerts in the UpdateSentAlertList ");
            int count = (int)result.Data!;
            Assert.True(count == 1, $" First call of PredictAlert has not sent only one email, count is {count}");

            result = await alertProcessor.PredictAlert();
            Assert.True(result.Success, $" 2nd Call to PredictAlert did not compete with success. {result.Message}");

            count = (int)result.Data!;
            // Assert
            Assert.True(count == 0, " Second call of PredictAlert has sent a second email for same alert.");

        }

        [Fact]
        public async Task MonitorEmailOff_TestSuccess()
        {
            //_rabbitRepoMock.Setup(repo => repo.PublishAsync<AlertServiceInitObj>("alertServiceReady", It.IsAny<AlertServiceInitObj>())).ReturnsAsync();
            _processorStateMock.Setup(p => p.EnabledProcessorList)
                                              .Returns(new List<ProcessorObj>());
            _emailProcessorMock.Setup(p => p.SendAlert(It.IsAny<AlertMessage>()))
                                             .ReturnsAsync(new ResultObj() { Success = true });
            _emailProcessorMock.Setup(p => p.VerifyEmail(It.IsAny<UserInfo>(), It.IsAny<IAlertable>())).Returns(true);
            _emailProcessorMock.Setup(p => p.VerifyEmail(It.Is<UserInfo>(u => u.UserID == "default"), It.Is<IAlertable>(a => a.ID == 4))).Returns(false);
            var alertParams=AlertTestData.GetAlertParams();
            alertParams.DisableEmailAlert=true;
            var alertProcessor = new AlertProcessor(_loggerAlertProcessorMock.Object, _rabbitRepoMock.Object, _emailProcessorMock.Object, _processorStateMock.Object, _netConnectCollectionMock.Object, alertParams, AlertTestData.GetUserInfos());
            // Act
            alertProcessor.MonitorAlertProcess.Alerts = AlertTestData.GetMonitorAlerts();

            var result = await alertProcessor.MonitorAlert();

            // Assert
            Assert.True(result.Success, $" Call to MonitorAlert check email off did not compete with success. {result.Message}");
            int count = (int)result.Data!;
            Assert.True(count == 0, $" Email sending is on. Count of send email was {count}");

           
        }

        [Fact]
        public async Task DataQueue_TestBadAuthKey()
        {
            var systemParams = AlertTestData.GetSystemParams();
            // Setup _systemParamsHelperMock to return the mocked SystemParams object from GetSystemParams()
            _systemParamsHelperMock.Setup(p => p.GetSystemParams()).Returns(systemParams);

            var dataQueueService = new DataQueueService(_loggerDataQueueMock.Object, _systemParamsHelperMock.Object);

            _processorStateMock.Setup(p => p.EnabledProcessorList)
                                             .Returns(new List<ProcessorObj>());

            var alertProcessor = new AlertProcessor(_loggerAlertProcessorMock.Object, _rabbitRepoMock.Object, _emailProcessorMock.Object, _processorStateMock.Object, _netConnectCollectionMock.Object, AlertTestData.GetAlertParams(), AlertTestData.GetUserInfos());
            // Act
            alertProcessor.PredictAlertProcess.Alerts = AlertTestData.GetPredictAlerts();
            var fileRepo = new FileRepo();


            var predictStatusAlerts = alertProcessor.PredictAlertProcess.Alerts;
            var processorDataObj = new ProcessorDataObj();
            processorDataObj.PredictStatusAlerts = alertProcessor.PredictAlerts;
            processorDataObj.AppID = "test";
            processorDataObj.AuthKey = "";
            var predictDataString = await fileRepo.SaveStateStringJsonZAsync<ProcessorDataObj>("TestProcessorDataObj", processorDataObj);
            var result = await dataQueueService.AddPredictDataStringToQueue(predictDataString, predictStatusAlerts);
            // Assert
            Assert.True(!result.Success, " Result was success with a bad auth key.");



        }
        [Fact]
        public async Task DataQueue_TestInvalidAppIDInData()
        {
            var systemParams = AlertTestData.GetSystemParams();
            // Setup _systemParamsHelperMock to return the mocked SystemParams object from GetSystemParams()
            _systemParamsHelperMock.Setup(p => p.GetSystemParams()).Returns(systemParams);

            var dataQueueService = new DataQueueService(_loggerDataQueueMock.Object, _systemParamsHelperMock.Object);

            _processorStateMock.Setup(p => p.EnabledProcessorList)
                                             .Returns(new List<ProcessorObj>());

            var alertProcessor = new AlertProcessor(_loggerAlertProcessorMock.Object, _rabbitRepoMock.Object, _emailProcessorMock.Object, _processorStateMock.Object, _netConnectCollectionMock.Object, AlertTestData.GetAlertParams(), AlertTestData.GetUserInfos());
            // Act
            alertProcessor.PredictAlertProcess.Alerts = AlertTestData.GetPredictAlerts();
            //Add bad data.
            AlertTestData.AddDataPredictAlerts(alertProcessor.PredictAlertProcess.Alerts);
            var fileRepo = new FileRepo();


            var predictStatusAlerts = alertProcessor.PredictAlertProcess.Alerts;
            var processorDataObj = new ProcessorDataObj();
            processorDataObj.PredictStatusAlerts = alertProcessor.PredictAlerts;
            processorDataObj.AppID = "test";
            processorDataObj.AuthKey = AesOperation.EncryptString(systemParams.EmailEncryptKey, processorDataObj.AppID);

            var predictDataString = await fileRepo.SaveStateStringJsonZAsync<ProcessorDataObj>("TestProcessorDataObj", processorDataObj);
            var result = await dataQueueService.AddPredictDataStringToQueue(predictDataString, predictStatusAlerts);
            // Assert
            Assert.True(!result.Success, " Result was success with a bad AppID.");

        }
        [Fact]
        public async Task DataQueue_TestPredictInput()
        {
            //_rabbitRepoMock.Setup(repo => repo.PublishAsync<AlertServiceInitObj>("alertServiceReady", It.IsAny<AlertServiceInitObj>())).ReturnsAsync();
            var systemParams = AlertTestData.GetSystemParams();
            // Setup _systemParamsHelperMock to return the mocked SystemParams object from GetSystemParams()
            _systemParamsHelperMock.Setup(p => p.GetSystemParams()).Returns(systemParams);

            var dataQueueService = new DataQueueService(_loggerDataQueueMock.Object, _systemParamsHelperMock.Object);

            _processorStateMock.Setup(p => p.EnabledProcessorList)
                                             .Returns(new List<ProcessorObj>());

            var alertProcessor = new AlertProcessor(_loggerAlertProcessorMock.Object, _rabbitRepoMock.Object, _emailProcessorMock.Object, _processorStateMock.Object, _netConnectCollectionMock.Object, AlertTestData.GetAlertParams(), AlertTestData.GetUserInfos());
            // Act
            alertProcessor.PredictAlertProcess.Alerts = AlertTestData.GetPredictAlerts();
            var fileRepo = new FileRepo();


            var originalPredictStatusAlerts = alertProcessor.PredictAlerts.Select(alert => new PredictStatusAlert(alert)).ToList();
            var predictStatusAlerts = alertProcessor.PredictAlertProcess.Alerts;
            var processorDataObj = new ProcessorDataObj();
            processorDataObj.PredictStatusAlerts = alertProcessor.PredictAlerts;
            processorDataObj.AppID = "test";
            processorDataObj.AuthKey = AesOperation.EncryptString(systemParams.EmailEncryptKey, processorDataObj.AppID);

            var predictDataString = await fileRepo.SaveStateStringJsonZAsync<ProcessorDataObj>("TestProcessorDataObj", processorDataObj);
            var result = await dataQueueService.AddPredictDataStringToQueue(predictDataString, predictStatusAlerts);
            // Assert
            Assert.True(result.Success, $" Result.Success was false. {result.Message}");
            var newPredictStatusAlerts = predictStatusAlerts.Select(alert => new PredictStatusAlert(alert)).ToList();

            Assert.Equal(originalPredictStatusAlerts, newPredictStatusAlerts, new PredictStatusAlertComparer());
            // Now create empty data to fill
            predictStatusAlerts = new List<IAlertable>();
            result = await dataQueueService.AddPredictDataStringToQueue(predictDataString, predictStatusAlerts);
            Assert.True(result.Success, $" Result.Success was false. {result.Message}");
            newPredictStatusAlerts = predictStatusAlerts.Select(alert => new PredictStatusAlert(alert)).ToList();

            Assert.Equal(originalPredictStatusAlerts, newPredictStatusAlerts, new PredictStatusAlertComparer());


        }

        [Fact]
        public async Task DataQueue_TestMonitorInput()
        {
            //_rabbitRepoMock.Setup(repo => repo.PublishAsync<AlertServiceInitObj>("alertServiceReady", It.IsAny<AlertServiceInitObj>())).ReturnsAsync();
            var systemParams = AlertTestData.GetSystemParams();
            // Setup _systemParamsHelperMock to return the mocked SystemParams object from GetSystemParams()
            _systemParamsHelperMock.Setup(p => p.GetSystemParams()).Returns(systemParams);

            var dataQueueService = new DataQueueService(_loggerDataQueueMock.Object, _systemParamsHelperMock.Object);

            _processorStateMock.Setup(p => p.EnabledProcessorList)
                                             .Returns(new List<ProcessorObj>());

            var alertProcessor = new AlertProcessor(_loggerAlertProcessorMock.Object, _rabbitRepoMock.Object, _emailProcessorMock.Object, _processorStateMock.Object, _netConnectCollectionMock.Object, AlertTestData.GetAlertParams(), AlertTestData.GetUserInfos());
            // Act
            alertProcessor.MonitorAlertProcess.Alerts = AlertTestData.GetMonitorAlerts();
            var fileRepo = new FileRepo();


            var originalMonitorStatusAlerts = alertProcessor.MonitorAlerts.Select(alert => new MonitorStatusAlert(alert)).ToList();
            var monitorStatusAlerts = alertProcessor.MonitorAlertProcess.Alerts;
            var processorDataObj = new ProcessorDataObj();
            processorDataObj.MonitorStatusAlerts = alertProcessor.MonitorAlerts;
            processorDataObj.AppID = "test";
            processorDataObj.AuthKey = AesOperation.EncryptString(systemParams.EmailEncryptKey, processorDataObj.AppID);

            var monitorDataString = await fileRepo.SaveStateStringJsonZAsync<ProcessorDataObj>("TestProcessorDataObj", processorDataObj);
            var result = await dataQueueService.AddProcessorDataStringToQueue(monitorDataString, monitorStatusAlerts);
            // Assert
            Assert.True(result.Success, $" Result.Success was false. {result.Message}");
            var newMonitorStatusAlerts = monitorStatusAlerts.Select(alert => new MonitorStatusAlert(alert)).ToList();
            Assert.Equal(originalMonitorStatusAlerts, newMonitorStatusAlerts, new MonitorStatusAlertComparer());
            // Now create empty data to fill
            monitorStatusAlerts = new List<IAlertable>();
            result = await dataQueueService.AddProcessorDataStringToQueue(monitorDataString, monitorStatusAlerts);
            Assert.True(result.Success, $" Result.Success was false. {result.Message}");
            newMonitorStatusAlerts = monitorStatusAlerts.Select(alert => new MonitorStatusAlert(alert)).ToList();

            Assert.Equal(originalMonitorStatusAlerts, newMonitorStatusAlerts, new MonitorStatusAlertComparer());


        }



    }

}
