
using NetworkMonitor.Objects;
using NetworkMonitor.Objects.ServiceMessage;
using NetworkMonitor.Objects.Repository;
using System;
using Microsoft.Extensions.Configuration;
using NetworkMonitor.Utils;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;
using NetworkMonitor.Utils.Helpers;
using NetworkMonitor.Connection;

namespace NetworkMonitor.Alert.Services
{
    public class AlertMessageService : IAlertMessageService
    {
        private IConfiguration _config;

        private List<UserInfo> _userInfos = new List<UserInfo>();

        private int _alertThreshold;
        //private bool _sendTrustPilot;
        private bool _alert;
        private bool _isAlertRunning = false;
        private bool _awake;
        private bool _checkAlerts;
        private bool _disableEmailAlert = false;
        private ILogger _logger;

        private IEmailProcessor _emailProcessor;
        private AlertProcessor _alertProcessor;

        private IDataQueueService _dataQueueService;


        //private List<ProcessorObj> _processorList = new List<ProcessorObj>();

        private IProcessorState _processorState;
        private IRabbitRepo _rabbitRepo;
        private IFileRepo _fileRepo;
        private ISystemParamsHelper _systemParamsHelper;
        private CancellationToken _token;
        private SystemParams _systemParams;
        public IRabbitRepo RabbitRepo { get => _rabbitRepo; }
        public bool IsAlertRunning { get => _isAlertRunning; set => _isAlertRunning = value; }
        public bool Awake { get => _awake; set => _awake = value; }
        public List<IAlertable> MonitorStatusAlerts { get => _alertProcessor.MonitorAlertProcess.Alerts; set => _alertProcessor.MonitorAlertProcess.Alerts = value; }
        public AlertMessageService(ILogger<AlertMessageService> logger, IConfiguration config, IDataQueueService dataQueueService, CancellationTokenSource cancellationTokenSource, IFileRepo fileRepo, IRabbitRepo rabbitRepo, ISystemParamsHelper systemParamsHelper, IProcessorState processorState)
        {
            _dataQueueService = dataQueueService;
            _fileRepo = fileRepo;
            _rabbitRepo = rabbitRepo;
            _logger = logger;
            _fileRepo.CheckFileExists("UserInfos", _logger);
            _config = config;
            _token = cancellationTokenSource.Token;
            _token.Register(() => OnStopping());
            _systemParamsHelper = systemParamsHelper;
            _processorState = processorState;

        }
        private void OnStopping()
        {
            ResultObj result = new ResultObj();
            result.Message = " SERVICE SHUTDOWN : starting shutdown of AlertMonitorService : ";
            try
            {
                result.Message += " Saving UserInfos into statestore. ";
                _fileRepo.SaveStateJsonZAsync<List<UserInfo>>("UserInfos", _userInfos);
                result.Message += " Saved UserInfos into statestore. ";
                result.Success = true;
                _logger.LogInformation(result.Message);
                _logger.LogWarning("SERVICE SHUTDOWN : Complete : ");
            }
            catch (Exception e)
            {
                _logger.LogCritical("Error : Failed to run Save Data before shutdown : Error Was : " + e.Message);
            }
        }
        public async Task Init()
        {

            AlertServiceInitObj alertObj = new AlertServiceInitObj();
            await InitService(alertObj);

        }
        public async Task InitService(AlertServiceInitObj alertObj)
        {
            var processorList = new List<ProcessorObj>();
            try
            {
                _fileRepo.CheckFileExists("ProcessorList", _logger);
                processorList = _fileRepo.GetStateJson<List<ProcessorObj>>("ProcessorList");


            }
            catch (Exception e)
            {
                _logger.LogError($" Error : Unable to get Processor List from State . Error was : {e.Message}");

            }

            if (processorList == null || processorList.Count == 0)
            {

                _logger.LogError(" Error : No processors in processor list .");
                processorList = new List<ProcessorObj>();
            }
            else
            {
                _logger.LogInformation($" Success : Got {processorList.Count} processors from state . ");
            }
            _processorState.ProcessorList = processorList;

            try
            {

                
                _systemParams = _systemParamsHelper.GetSystemParams();
                _logger.LogDebug("SystemParams: " + JsonUtils.WriteJsonObjectToString(_systemParams));
                _logger.LogDebug("PingAlertThreshold: " + _alertThreshold);


                _emailProcessor = new EmailProcessor(_systemParams, _logger, _disableEmailAlert);
                _logger.LogInformation("Got config");
            }
            catch (Exception e)
            {
                _logger.LogError("Error : Can not get Config" + e.Message.ToString());
            }
            if (alertObj.TotalReset)
            {
                try
                {
                    _logger.LogInformation("Resetting Alert UserInfos in statestore");
                    _userInfos = new List<UserInfo>();
                    await _fileRepo.SaveStateJsonZAsync<List<UserInfo>>("UserInfos", _userInfos);
                    _logger.LogInformation("Reset UserInfos in statestore ");
                }
                catch (Exception e)
                {
                    _logger.LogError("Error : Can not reset UserInfos in statestre Error was : " + e.Message.ToString());
                }
            }
            else
            {
                if (alertObj.UpdateUserInfos)
                {
                    _userInfos = alertObj.UserInfos;
                    try
                    {
                        await _fileRepo.SaveStateJsonZAsync<List<UserInfo>>("UserInfos", _userInfos);
                        _logger.LogInformation("Saved " + _userInfos.Count() + " UserInfos to statestore ");
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("Error : Can not save UserInfos to statestore Error was : " + e.Message.ToString());
                    }
                }
                else
                {
                    try
                    {
                        var userInfos = _fileRepo.GetStateJsonZAsync<List<UserInfo>>("UserInfos").Result;
                        if (userInfos == null) _userInfos = new List<UserInfo>();
                        else
                        {
                            _userInfos = userInfos;
                            _logger.LogInformation("Got " + _userInfos.Count() + "  UserInfos from statestore ");
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("Error : Can not get UserInfos from statestore Error was : " + e.Message.ToString());
                    }
                }
                if (_userInfos != null && _userInfos.Count() != 0)
                {
                    _logger.LogInformation("Got UserInfos " + _userInfos.Count + " from published message ");
                }
                else
                {
                    _logger.LogWarning("Warning got no UserInfos from state file.");
                }
            }
            try
            {
                alertObj.IsAlertServiceReady = true;
                await _rabbitRepo.PublishAsync<AlertServiceInitObj>("alertServiceReady", alertObj);
                _logger.LogInformation("Published event AlertServiceItitObj.IsAlertServiceReady = true");
            }
            catch (Exception e)
            {
                _logger.LogError("Error : Can not publish event  AlertServiceItitObj.IsAlertServiceReady Error was : " + e.Message.ToString());
            }
            try
            {
                var alertParams= _systemParamsHelper.GetAlertParams();
                var netConnectConfig = new NetConnectConfig(_config);
                var connectFactory = new ConnectFactory(_logger, false);
                var netConnectCollection = new NetConnectCollection(_logger, netConnectConfig, connectFactory);

                _alertProcessor = new AlertProcessor(_logger, _rabbitRepo, _emailProcessor, _processorState, netConnectCollection, alertParams, _userInfos);

            }
            catch (Exception e)
            {
                _logger.LogError("Error : unable to setup AlertProcessor . Error was : " + e.Message.ToString());

            }
        }

        public async Task<ResultObj> MonitorAlert()
        {
            return await _alertProcessor.MonitorAlert();
        }
        public async Task<ResultObj> PredictAlert()
        {
            return await _alertProcessor.PredictAlert();
        }

        public bool IsBadAuthKey(string authKey, string appID)
        {
            return EncryptionHelper.IsBadKey(_systemParams.EmailEncryptKey, authKey, appID);
        }

        public async Task<ResultObj> Send(AlertMessage alertMessage)
        {
            return await _emailProcessor.SendAlert(alertMessage);
        }

        public async Task<ResultObj> SendGenericEmail(GenericEmailObj genericEmail)
        {
            return await _emailProcessor.SendGenericEmail(genericEmail);
        }

        public async Task<ResultObj> SendHostReport(HostReportObj hostReport)
        {

            return await _emailProcessor.SendHostReport(hostReport);
        }

        public async Task<List<ResultObj>> UserHostExpire(List<GenericEmailObj> emailObjs)
        {
            return await _emailProcessor.UserHostExpire(emailObjs);
        }
        public async Task<List<ResultObj>> UpgradeAccounts(List<GenericEmailObj> emailObjs)
        {
            return await _emailProcessor.UpgradeAccounts(emailObjs);
        }

        public async Task<ResultObj> WakeUp()
        {
            ResultObj result = new ResultObj();
            result.Message = "SERVICE : AlertMessageService.WakeUp() ";
            try
            {
                if (_awake)
                {
                    result.Message += "Received WakeUp but Alert is currently running";
                    result.Success = false;
                }
                else
                {
                    AlertServiceInitObj alertObj = new AlertServiceInitObj();
                    alertObj.IsAlertServiceReady = true;
                    await _rabbitRepo.PublishAsync<AlertServiceInitObj>("alertServiceReady", alertObj);
                    result.Message += "Received WakeUp so Published event AlertServiceItitObj.IsAlertServiceReady = true";
                    result.Success = true;
                }
            }
            catch (Exception e)
            {
                result.Message += "Error : failed to Published event AlertServiceItitObj.IsAlertServiceReady = true. Error was : " + e.ToString();
                result.Success = false;
            }
            return result;
        }

        public async Task<ResultObj> UpdateUserInfo(UserInfo userInfo)
        {
            var result = new ResultObj();
            try
            {
                if (_userInfos.Where(w => w.UserID == userInfo.UserID).Count() != 0)
                {
                    UserInfo? newUserInfo = _userInfos.FirstOrDefault(w => w.UserID == userInfo.UserID);
                    if (newUserInfo != null) _userInfos.Remove(newUserInfo);
                }
                _userInfos.Add(userInfo);
                try
                {
                    await _fileRepo.SaveStateJsonZAsync<List<UserInfo>>("UserInfos", _userInfos);
                    _logger.LogInformation("Saved UserInfos to file statestore ");
                }
                catch (Exception e)
                {
                    _logger.LogError("Error : Can not save UserInfos to statestore Error was : " + e.Message.ToString());
                }
                result.Success = true;
                result.Message = ("Success : updated UserInfo");
            }
            catch (Exception e)
            {
                result.Success = false;
                result.Message = "Failed : could not update UserInfo for userId " + userInfo.UserID + " . Error was :" + e.Message.ToString();
            }
            return result;
        }
        public List<ResultObj> ResetMonitorAlerts(List<AlertFlagObj> alertFlagObjs)
        {
            return _alertProcessor.ResetMonitorAlerts(alertFlagObjs);
        }
         public List<ResultObj> ResetPredictAlerts(List<AlertFlagObj> alertFlagObjs)
        {
            return _alertProcessor.ResetPredictAlerts(alertFlagObjs);
        }
    }

}
