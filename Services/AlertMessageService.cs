using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Hosting;
using MimeKit;
using NetworkMonitor.Objects;
using NetworkMonitor.Objects.ServiceMessage;
using NetworkMonitor.Objects.Repository;
using NetworkMonitor.Connection;
using NetworkMonitor.Alert.Services.Helpers;
using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetworkMonitor.Utils;
using System.Linq;
using System.Web;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using NetworkMonitor.Utils.Helpers;

namespace NetworkMonitor.Alert.Services
{
    public class AlertMessageService : IAlertMessageService
    {
        private IConfiguration _config;

        private List<UserInfo> _userInfos = new List<UserInfo>();

        private int _alertThreshold;
        private bool _sendTrustPilot;
        private bool _alert;
        private bool _isAlertRunning = false;
        private bool _awake;
        private bool _checkAlerts;
        private bool _disableEmailAlert = false;
        private ILogger _logger;

        private EmailProcessor _emailProcessor;
        List<MonitorStatusAlert> _updateAlertSentList = new List<MonitorStatusAlert>();
        private IDataQueueService _dataQueueService;
        private List<AlertMessage> _alertMessages = new List<AlertMessage>();
        private List<MonitorStatusAlert> _monitorStatusAlerts = new List<MonitorStatusAlert>();
        private List<ProcessorObj> _processorList = new List<ProcessorObj>();
        private IRabbitRepo _rabbitRepo;
        private IFileRepo _fileRepo;
        private ISystemParamsHelper _systemParamsHelper;
        private CancellationToken _token;
        public IRabbitRepo RabbitRepo { get => _rabbitRepo; }
        public bool IsAlertRunning { get => _isAlertRunning; set => _isAlertRunning = value; }
        public bool Awake { get => _awake; set => _awake = value; }
        public List<MonitorStatusAlert> MonitorStatusAlerts { get => _monitorStatusAlerts; set => _monitorStatusAlerts = value; }
        public AlertMessageService(ILogger<AlertMessageService> logger, IConfiguration config, IDataQueueService dataQueueService, CancellationTokenSource cancellationTokenSource, IFileRepo fileRepo, IRabbitRepo rabbitRepo, ISystemParamsHelper systemParamsHelper)
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
        public Task Init()
        {
            return Task.Run(() =>
              {
                  AlertServiceInitObj alertObj = new AlertServiceInitObj();
                  InitService(alertObj);
              });
        }
        public void InitService(AlertServiceInitObj alertObj)
        {
            try
            {

                _alertThreshold = _config.GetValue<int>("PingAlertThreshold");
                _checkAlerts = _config.GetValue<bool>("CheckAlerts");
                _disableEmailAlert = _config.GetValue<bool>("DisableEmailAlert") ;
                SystemParams systemParams = _systemParamsHelper.GetSystemParams();
                _processorList = new List<ProcessorObj>();
                _config.GetSection("ProcessorList").Bind(_processorList);
                _logger.LogDebug("SystemParams: " + JsonUtils.writeJsonObjectToString(systemParams));
                _logger.LogDebug("PingAlertThreshold: " + _alertThreshold);

                _sendTrustPilot = systemParams.SendTrustPilot;
                _emailProcessor = new EmailProcessor(systemParams, _logger,_disableEmailAlert);
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
                    _fileRepo.SaveStateJsonZAsync<List<UserInfo>>("UserInfos", _userInfos);
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
                        _fileRepo.SaveStateJsonZAsync<List<UserInfo>>("UserInfos", _userInfos);
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
                        _userInfos = _fileRepo.GetStateJsonZAsync<List<UserInfo>>("UserInfos").Result;
                        if (_userInfos == null) _userInfos = new List<UserInfo>();
                        else
                        {
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
                _rabbitRepo.Publish<AlertServiceInitObj>("alertServiceReady", alertObj);
                _logger.LogInformation("Published event AlertServiceItitObj.IsAlertServiceReady = true");
            }
            catch (Exception e)
            {
                _logger.LogError("Error : Can not publish event  AlertServiceItitObj.IsAlertServiceReady Error was : " + e.Message.ToString());
            }
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

        public async Task<List<ResultObj>> UserHostExpire(List<UserInfo> userInfos)
        {
            return await _emailProcessor.UserHostExpire(userInfos);
        }
        private void VerifyEmail(UserInfo userInfo, MonitorStatusAlert monitorStatusAlert)
        {
            // Validate email format
            if (monitorStatusAlert.AddUserEmail != null)
            {


                var emailRegex = new Regex(@"^[\w-+]+(\.[\w-]+)*@([\w-]+\.)+[a-zA-Z]{2,7}$");
                if (emailRegex.IsMatch(monitorStatusAlert.AddUserEmail))
                {

                    //_logger.LogInformation(" Success : Rewriting email address from " + userInfo.Email + " to " + monitorStatusAlert.AddUserEmail);
                    userInfo.Email = monitorStatusAlert.AddUserEmail;
                    userInfo.DisableEmail = !monitorStatusAlert.IsEmailVerified;
                }
                else
                {
                    userInfo.DisableEmail = true;
                    // Handle invalid email format
                    _logger.LogWarning(" Warning : Invalid email format: " + monitorStatusAlert.AddUserEmail);
                }
            }
            else
            {
                userInfo.DisableEmail = !userInfo.Email_verified;
            }
        }

        public ResultObj WakeUp()
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
                    _rabbitRepo.Publish<AlertServiceInitObj>("alertServiceReady", alertObj);
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
        public async Task<ResultObj> Alert()
        {
            _awake = true;
            ResultObj result = new ResultObj();
            result.Message = "SERVICE : AlertMessageService.Alert() ";
            AlertServiceInitObj alertObj = new AlertServiceInitObj();
            result.Success = false;
            alertObj.IsAlertServiceReady = false;
            await _rabbitRepo.PublishAsync<AlertServiceInitObj>("alertServiceReady", alertObj);
            _logger.LogInformation("Published event AlertServiceItitObj.IsAlertServiceReady = false");
            Stopwatch timerInner = new Stopwatch();
            timerInner.Start();
            try
            {
                result.Message += await InitAlerts(_userInfos);
                int count = await SendAlerts();
                result.Message += "Success : AlertMessageService.Alert Executed ";
                if (_alert)
                {
                    result.Message += "Info : Message sent was to :" + count + " users";
                }
                result.Success = true;
                _logger.LogInformation(result.Message);
            }
            catch (Exception e)
            {
                result.Message += "Error : AlertMessageService.Alert Execute failed : Error was : " + e.ToString();
                result.Success = false;
                _logger.LogError(result.Message);
            }
            finally
            {
                try
                {
                    TimeSpan timeTakenInner = timerInner.Elapsed;
                    // If time taken is greater than the time to wait, then we need to adjust the time to wait.
                    int timeTakenInnerInt = (int)timeTakenInner.TotalMilliseconds;
                    if (timeTakenInnerInt < 10000)
                    {
                        _logger.LogInformation("Sleeping for " + (10000 - timeTakenInnerInt) + " ms to allow message to pass to scheduler");
                        new System.Threading.ManualResetEvent(false).WaitOne(10000 - timeTakenInnerInt);
                    }
                    alertObj.IsAlertServiceReady = true;
                    await _rabbitRepo.PublishAsync<AlertServiceInitObj>("alertServiceReady", alertObj);
                    _logger.LogInformation("Published event AlertServiceItitObj.IsAlertServiceReady = true");
                }
                catch (Exception e)
                {
                    _logger.LogError("Error : Can not publish event  AlertServiceItitObj.IsAlertServiceReady " + e.Message.ToString().ToString());
                }
                _awake = false;
            }
            return result;
        }

        public async Task<String> InitAlerts(List<UserInfo> userInfos)
        {
            string resultStr = " InitAlerts : ";
            _alert = false;
            _alertMessages = new List<AlertMessage>();
            var updateAlertFlagList = new List<MonitorStatusAlert>();
            var publishAlertSentList = new List<MonitorStatusAlert>();
            while (_isAlertRunning)
            {
                resultStr += " Warning : Waiting for Alert to stop running ";
                new System.Threading.ManualResetEvent(false).WaitOne(1000);
            }
            _isAlertRunning = true;
            var monitorStatusAlerts = _monitorStatusAlerts.ConvertAll(c => new MonitorStatusAlert(c));
            _isAlertRunning = false;
            foreach (MonitorStatusAlert monitorStatusAlert in monitorStatusAlerts)
            {
                bool noAlertSentStored = _updateAlertSentList.FirstOrDefault(w => w.ID == monitorStatusAlert.ID) == null;
                if (monitorStatusAlert.AlertFlag = true && monitorStatusAlert.AlertSent == false && !noAlertSentStored) publishAlertSentList.Add(monitorStatusAlert);
                string userId = monitorStatusAlert.UserID;
                UserInfo userInfo = new UserInfo(userInfos.FirstOrDefault(u => u.UserID == userId));

                if (userInfo.UserID == "default")
                {
                    VerifyEmail(userInfo, monitorStatusAlert);
                    userInfo.Name = userInfo.Email.Split('@')[0];
                    userId = userInfo.Email;
                    userInfo.UserID = userId;
                }
                else
                {
                    if (!userInfo.DisableEmail)
                    {
                        VerifyEmail(userInfo, monitorStatusAlert);
                    }
                }

                if (monitorStatusAlert.AddUserEmail == "delete") userInfo.DisableEmail = true;

                monitorStatusAlert.UserName = userInfo.Name;
                if (monitorStatusAlert.DownCount > _alertThreshold && monitorStatusAlert.AlertSent == false && noAlertSentStored)
                {
                    // Its not the first messge for this user so we need to add a new line
                    if (_alertMessages.FirstOrDefault(a => a.UserID == userId) != null)
                    {
                        var alertMessage = _alertMessages.FirstOrDefault(a => a.UserID == userId);
                        alertMessage.Message += "\n" + monitorStatusAlert.EndPointType.ToUpper() + " Alert for host at address " + monitorStatusAlert.Address + " status message is " + monitorStatusAlert.Message + " . " +
                                                   "\nHost down count is " + monitorStatusAlert.DownCount + "\nThe time of this event is  " + monitorStatusAlert.EventTime + "\n" +
                                                   " The Processing server ID was " + monitorStatusAlert.AppID + " The timeout was set to " + monitorStatusAlert.Timeout + " ms. \n\n";
                        alertMessage.AlertFlagObjs.Add(monitorStatusAlert);
                    }
                    // This is the first message for this user so we need to add a new AlertMessage.                   
                    else
                    {
                        var alertMessage = new AlertMessage();
                        alertMessage.UserInfo = userInfo; //  There is a problem this user is not in the database.
                        if (userInfo == null)
                        {
                            resultStr += "Warning : UserID " + userId + " not found in UserInfo table";
                        }
                        else
                        {
                            // Add start message
                            alertMessage.Message = "Alert message for " + monitorStatusAlert.UserName + " . ";
                            alertMessage.Message += "\n" + monitorStatusAlert.EndPointType.ToUpper() + " Alert for host at address " + monitorStatusAlert.Address + " status message is " + monitorStatusAlert.Message + " . " +
                                      "\nHost down count is " + monitorStatusAlert.DownCount + "\nThe time of this event is  " + monitorStatusAlert.EventTime + "\n" +
                                      " The Processing server ID was " + monitorStatusAlert.AppID + " The timeout was set to " + monitorStatusAlert.Timeout + " ms. \n\n";
                            alertMessage.dontSend = userInfo.DisableEmail;
                            alertMessage.AlertFlagObjs.Add(monitorStatusAlert);
                            _alertMessages.Add(alertMessage);
                        }
                    }
                    updateAlertFlagList.Add(monitorStatusAlert);
                    //monitorStatusAlert.AlertFlag = true;
                }
            }
            if (publishAlertSentList.Count() != 0)
            {
                _logger.LogWarning("Warning republishing AlertSent List check coms. ");
                await PublishAlertsRepo.ProcessorAlertSent(_logger, _rabbitRepo, publishAlertSentList, _processorList);
            }
            await CheckAlerts(updateAlertFlagList);
            if (updateAlertFlagList.Count() > 0)
            {
                _alert = true;
                await PublishAlertsRepo.ProcessorAlertFlag(_logger, _rabbitRepo, updateAlertFlagList, _processorList);
            }
            else _alert = false;
            return resultStr;
        }
        private async Task CheckAlerts(List<MonitorStatusAlert> updateAlertFlagList)
        {
            if (updateAlertFlagList == null || updateAlertFlagList.Count() == 0) return;
            if (!_checkAlerts) return;
            var pingParams = new PingParams();
            var monitorPingInfos = new List<MonitorPingInfo>();
            int maxTimeout = 0;
            // exclude MonitorPingInfos that have EndPointType set to string values in ExcludeEndPointTypList
            var excludeEndPoints = new ExcludeEndPointTypeList();
            updateAlertFlagList.Where(w => !excludeEndPoints.Contains(w.EndPointType)).ToList().ForEach(a =>
            {
                monitorPingInfos.Add(new MonitorPingInfo()
                {
                    ID = a.ID,
                    MonitorIPID = a.ID,
                    Address = a.Address,
                    AppID = a.AppID,
                    EndPointType = a.EndPointType,
                    Timeout = a.Timeout,
                    Enabled = true
                });
                if (a.Timeout > maxTimeout) maxTimeout = a.Timeout;
            });
            _logger.LogInformation(" Checking " + monitorPingInfos.Count() + " Alerts ");
            var connectFactory = new ConnectFactory(_config, _logger, false);
            var netConnectCollection = new NetConnectCollection(_logger, _config, connectFactory);
            SemaphoreSlim semaphore = new SemaphoreSlim(1);
            netConnectCollection.NetConnectFactory(monitorPingInfos, pingParams, true, false, semaphore).Wait();
            var netConnects = netConnectCollection.GetNonLongRunningNetConnects().ToList();
            var pingConnectTasks = new List<Task>();
            netConnects.Where(w => w.MpiStatic.Enabled == true).ToList().ForEach(
                netConnect =>
                {
                    pingConnectTasks.Add(netConnect.Connect());
                }
            );
            Task.WhenAll(pingConnectTasks.ToArray()).Wait();
            //new System.Threading.ManualResetEvent(false).WaitOne(maxTimeout);
            var monitorIPDic = new Dictionary<string, List<int>>();
            monitorPingInfos.Where(w => w.MonitorStatus.IsUp == true).ToList().ForEach(m =>
           {
               updateAlertFlagList.RemoveAll(r => r.ID == m.MonitorIPID);
               _logger.LogWarning(" Warning : Overturned Alert with MonitorPingID = " + m.MonitorIPID + " . On Processor with AppID " + m.AppID + " . ");
               _alertMessages.ForEach(a =>
               {
                   a.AlertFlagObjs.RemoveAll(r => r.ID == m.MonitorIPID);
               });
               if (!monitorIPDic.ContainsKey(m.AppID))
               {
                   monitorIPDic.Add(m.AppID, new List<int>() { m.MonitorIPID });
               }
               else
               {
                   monitorIPDic[m.AppID].Add(m.MonitorIPID);
               }
           });
            _alertMessages.RemoveAll(r => r.AlertFlagObjs.Count() == 0);
            await PublishAlertsRepo.ProcessorResetAlerts(_logger, _rabbitRepo, monitorIPDic);
        }

        public async Task<int> SendAlerts()
        {
            int count = 0;
            if (_alert)
            {
                var publishAlertSentList = new List<MonitorStatusAlert>();
                foreach (AlertMessage alertMessage in _alertMessages)
                {
                    alertMessage.SendTrustPilot = _sendTrustPilot;
                    alertMessage.Subject = "Network Monitor Alert you have a Host down";
                    if (!alertMessage.dontSend)
                    {
                        alertMessage.VerifyLink = false;
                        var result = new ResultObj();
                        result = await _emailProcessor.SendAlert(alertMessage);

                        if (result.Success)
                        {
                            result.Message += " Success : Sent alert message to " + alertMessage.EmailTo;
                            UpdateAndPublishAlertSentList(alertMessage, publishAlertSentList);
                            _logger.LogInformation(result.Message);
                        }
                        else
                        {
                            _logger.LogError(result.Message);
                        }

                        count++;
                    }
                    else
                    {
                        UpdateAndPublishAlertSentList(alertMessage, publishAlertSentList);
                    }
                }
                await PublishAlertsRepo.ProcessorAlertSent(_logger, _rabbitRepo, publishAlertSentList, _processorList);
            }
            return count;
        }

        private void UpdateAndPublishAlertSentList(AlertMessage alertMessage, List<MonitorStatusAlert> publishAlertSentList)
        {
            foreach (MonitorStatusAlert alertFlagObj in alertMessage.AlertFlagObjs)
            {
                alertFlagObj.AlertSent = true;
                publishAlertSentList.Add(alertFlagObj);
                _updateAlertSentList.Add(alertFlagObj);
                //var updateMonitorStatusAlert = _monitorStatusAlerts.FirstOrDefault(w => w.ID == alertFlagObj.ID);
                //if (updateMonitorStatusAlert != null) updateMonitorStatusAlert.AlertSent = true;
            }
        }
        public List<ResultObj> ResetAlerts(List<AlertFlagObj> alertFlagObjs)
        {
            var results = new List<ResultObj>();
            var result = new ResultObj();
            alertFlagObjs.ForEach(f =>
            {
                try
                {
                    while (_isAlertRunning)
                    {
                        result.Message += " Info : Waiting for Alert to stop running ";
                        new System.Threading.ManualResetEvent(false).WaitOne(5000);
                    }
                    var updateMonitorStatusAlert = _monitorStatusAlerts.FirstOrDefault(w => w.ID == f.ID && w.AppID == f.AppID);
                    if (updateMonitorStatusAlert == null)
                    {
                        result.Success = false;
                        result.Message += " Warning : Unable to find MonitorStatusAlert with ID " + f.ID + " with AppID " + f.AppID + " . ";
                    }
                    else
                    {
                        updateMonitorStatusAlert.AlertFlag = false;
                        updateMonitorStatusAlert.AlertSent = false;
                        updateMonitorStatusAlert.DownCount = 0;
                        result.Success = true;
                        result.Message += " Success : updated MonitorStatusAlert with ID " + f.ID + " with AppID " + f.AppID + " . ";
                    }
                    _updateAlertSentList.RemoveAll(r => r.ID == f.ID);
                }
                catch (Exception e)
                {
                    result.Success = false;
                    result.Message += " Error : Unable to reset alerts for MonitorStatusAlert with ID " + f.ID + " with AppID " + f.AppID + " Error was : " + e.Message + " . ";
                }
                results.Add(result);
            });
            return results;
        }
        public async Task<ResultObj> UpdateUserInfo(UserInfo userInfo)
        {
            var result = new ResultObj();
            try
            {
                if (_userInfos.Where(w => w.UserID == userInfo.UserID).Count() != 0)
                {
                    UserInfo newUserInfo = _userInfos.FirstOrDefault(w => w.UserID == userInfo.UserID);
                    _userInfos.Remove(newUserInfo);
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
    }
}
