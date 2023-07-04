using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Hosting;
using MimeKit;
using NetworkMonitor.Objects;
using NetworkMonitor.Objects.ServiceMessage;
using NetworkMonitor.Objects.Repository;
using NetworkMonitor.Connection;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetworkMonitor.Utils;
using System.Linq;
using System.Web;
using System.Collections.Generic;
using MetroLog;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using NetworkMonitor.Utils.Helpers;
using NetworkMonitor.Objects.Factory;
using HostInitActions;
namespace NetworkMonitor.Alert.Services
{
    public class AlertMessageService : IAlertMessageService
    {
        private IConfiguration _config;
        private ILogger _logger;
        private List<UserInfo> _userInfos = new List<UserInfo>();
        private string _emailEncryptKey;
        private string _systemEmail;
        private string _systemUrl;
        private string _systemPassword;
        private string _systemUser;
        private string _trustPilotReviewEmail;
        private string _mailServer;
        private int _mailServerPort;
        private bool _mailServerUseSSL;
        private SystemUrl _thisSystemUrl;
        private string _publicIPAddress;
        private int _alertThreshold;
        private bool _sendTrustPilot;
        private bool _alert;
        private bool _isAlertRunning = false;
        private bool _awake;
        private bool _checkAlerts;
        private SpamFilter _spamFilter;
        List<MonitorStatusAlert> _updateAlertSentList = new List<MonitorStatusAlert>();
        private IDataQueueService _dataQueueService;
        private List<AlertMessage> _alertMessages = new List<AlertMessage>();
        private List<MonitorStatusAlert> _monitorStatusAlerts = new List<MonitorStatusAlert>();
        private List<ProcessorObj> _processorList = new List<ProcessorObj>();
        private RabbitListener _rabbitRepo;
        private CancellationToken _token;
        public RabbitListener RabbitRepo { get => _rabbitRepo; }
        public bool IsAlertRunning { get => _isAlertRunning; set => _isAlertRunning = value; }
        public bool Awake { get => _awake; set => _awake = value; }
        public List<MonitorStatusAlert> MonitorStatusAlerts { get => _monitorStatusAlerts; set => _monitorStatusAlerts = value; }
        public AlertMessageService(INetLoggerFactory loggerFactory, IConfiguration config, IDataQueueService dataQueueService, CancellationTokenSource cancellationTokenSource)
        {
            _dataQueueService = dataQueueService;
            _logger = loggerFactory.GetLogger("AlertMessageService");
            FileRepo.CheckFileExists("UserInfos", _logger);
            _config = config;
            _token = cancellationTokenSource.Token;
            _token.Register(() => OnStopping());
            _spamFilter = new SpamFilter(_logger);
        }
        private void OnStopping()
        {
            ResultObj result = new ResultObj();
            result.Message = " SERVICE SHUTDOWN : starting shutdown of AlertMonitorService : ";
            try
            {
                result.Message += " Saving UserInfos into statestore. ";
                FileRepo.SaveStateJsonZ<List<UserInfo>>("UserInfos", _userInfos);
                result.Message += " Saved UserInfos into statestore. ";
                result.Success = true;
                _logger.Info(result.Message);
                _logger.Warn("SERVICE SHUTDOWN : Complete : ");
            }
            catch (Exception e)
            {
                _logger.Fatal("Error : Failed to run Save Data before shutdown : Error Was : " + e.Message);
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
                SystemParams systemParams = SystemParamsHelper.getSystemParams(_config, _logger);
                _processorList = new List<ProcessorObj>();
                _config.GetSection("ProcessorList").Bind(_processorList);
                _logger.Debug("SystemParams: " + JsonUtils.writeJsonObjectToString(systemParams));
                _logger.Debug("PingAlertThreshold: " + _alertThreshold);
                _emailEncryptKey = systemParams.EmailEncryptKey;
                _systemEmail = systemParams.SystemEmail;
                _systemUser = systemParams.SystemUser;
                _systemPassword = systemParams.SystemPassword;
                _mailServer = systemParams.MailServer;
                _mailServerPort = systemParams.MailServerPort;
                _mailServerUseSSL = systemParams.MailServerUseSSL;
                _trustPilotReviewEmail = systemParams.TrustPilotReviewEmail;
                _thisSystemUrl = systemParams.ThisSystemUrl;
                _publicIPAddress = systemParams.PublicIPAddress;
                _sendTrustPilot = systemParams.SendTrustPilot;
                try
                {
                    _rabbitRepo = new RabbitListener(_logger, systemParams.ThisSystemUrl, this, _dataQueueService);
                }
                catch (Exception e)
                {
                    _logger.Fatal(" Could not setup RabbitListner. Error was : " + e.ToString() + " . ");
                }
                _logger.Info("Got config");
            }
            catch (Exception e)
            {
                _logger.Error("Error : Can not get Config" + e.Message.ToString());
            }

            if (alertObj.TotalReset)
            {
                try
                {
                    _logger.Info("Resetting Alert UserInfos in statestore");
                    _userInfos = new List<UserInfo>();
                    FileRepo.SaveStateJsonZ<List<UserInfo>>("UserInfos", _userInfos);
                    _logger.Info("Reset UserInfos in statestore ");
                }
                catch (Exception e)
                {
                    _logger.Error("Error : Can not reset UserInfos in statestre Error was : " + e.Message.ToString());
                }
            }
            else
            {
                if (alertObj.UpdateUserInfos)
                {
                    _userInfos = alertObj.UserInfos;
                    try
                    {
                        FileRepo.SaveStateJsonZ<List<UserInfo>>("UserInfos", _userInfos);
                        _logger.Info("Saved "+_userInfos.Count()+" UserInfos to statestore ");
                    }
                    catch (Exception e)
                    {
                        _logger.Error("Error : Can not save UserInfos to statestore Error was : " + e.Message.ToString());
                    }
                }
                else
                {
                    try
                    {
                        _userInfos = FileRepo.GetStateJsonZ<List<UserInfo>>("UserInfos");
                        _logger.Info("Got "+_userInfos.Count()+"  UserInfos from statestore ");
                        if (_userInfos == null) _userInfos = new List<UserInfo>();
                    }
                    catch (Exception e)
                    {
                        _logger.Error("Error : Can not get UserInfos from statestore Error was : " + e.Message.ToString());
                    }
                }
                if (_userInfos.Count() != 0)
                {
                    _logger.Info("Got UserInfos " + _userInfos.Count + " from published message ");
                }
                else
                {
                    _logger.Warn("Warning got zero UserInfos ");
                }
            }

            try
            {
                alertObj.IsAlertServiceReady = true;
                _rabbitRepo.Publish<AlertServiceInitObj>("alertServiceReady", alertObj);
                _logger.Info("Published event AlertServiceItitObj.IsAlertServiceReady = true");
            }
            catch (Exception e)
            {
                _logger.Error("Error : Can not publish event  AlertServiceItitObj.IsAlertServiceReady Error was : " + e.Message.ToString());
            }
        }
       
        public string EncryptStr(string str)
        {
            str=AesOperation.EncryptString(_emailEncryptKey, str);
            return HttpUtility.UrlEncode(str);
        }
        public ResultObj Send(AlertMessage alertMessage)
        {
            ResultObj result = new ResultObj();
            result = _spamFilter.IsVerifyLimit(alertMessage.UserInfo.UserID);
            if (!result.Success)
            {
                return result;
            }
            string enryptEmailAddressStr = EncryptStr(alertMessage.UserInfo.Email);
            string enryptUserID = EncryptStr(alertMessage.UserInfo.UserID);
            string subscribeUrl = _thisSystemUrl.ExternalUrl + "/email/unsubscribe?email=" + enryptEmailAddressStr + "&userid=" + enryptUserID;
            string resubscribeUrl = subscribeUrl + "&subscribe=true";
            string unsubscribeUrl = subscribeUrl + "&subscribe=false";
            if (alertMessage.VerifyLink)
            {
                string verifyUrl = _thisSystemUrl.ExternalUrl + "/email/verifyemail?email=" + enryptEmailAddressStr + "&userid=" + enryptUserID;
                alertMessage.Message += "\n\nPlease click on this link to verify your email " + verifyUrl;
            }
            alertMessage.Message += "\n\nThis message was sent by the messenger running at " + _thisSystemUrl.ExternalUrl + " (" + _publicIPAddress.ToString() + ")\n\n To unsubscribe from receiving these messages, please click this link " + unsubscribeUrl + "\n\n To re-subscribe to receiving these messages, please click this link " + resubscribeUrl;
            string emailFrom = _systemEmail;
            string systemPassword = _systemPassword;
            string systemUser = _systemUser;
            int mailServerPort = _mailServerPort;
            bool mailServerUseSSL = _mailServerUseSSL;
            try
            {
                MimeMessage message = new MimeMessage();
                message.Headers.Add("List-Unsubscribe", "<" + unsubscribeUrl + ">, <mailto:" + emailFrom + "?subject=unsubscribe>");
                MailboxAddress from = new MailboxAddress("Free Network Monitor",
                emailFrom);
                message.From.Add(from);
                if (alertMessage.SendTrustPilot)
                {
                    MailboxAddress bcc = new MailboxAddress("Trust Pilot",
             _trustPilotReviewEmail);
                    message.Bcc.Add(bcc);
                }
                MailboxAddress to = new MailboxAddress(alertMessage.Name,
                alertMessage.EmailTo);
                message.To.Add(to);
                //message.Subject = "Network Monitor Alert : Host Down";
                message.Subject = alertMessage.Subject;
                BodyBuilder bodyBuilder = new BodyBuilder();
                bodyBuilder.TextBody = alertMessage.Message;
                //bodyBuilder.Attachments.Add(_env.WebRootPath + "\\file.png");
                message.Body = bodyBuilder.ToMessageBody();
                SmtpClient client = new SmtpClient();
                client.ServerCertificateValidationCallback = (mysender, certificate, chain, sslPolicyErrors) => { return true; };
                client.CheckCertificateRevocation = false;
                if (mailServerUseSSL)
                {
                    client.Connect(_mailServer, mailServerPort, true);
                }
                else
                {
                    client.Connect(_mailServer, mailServerPort, MailKit.Security.SecureSocketOptions.StartTls);
                }
                client.Authenticate(systemUser, systemPassword);
                client.Send(message);
                client.Disconnect(true);
                client.Dispose();
                result.Message = "Email with subject " + alertMessage.Subject + " sent ok";
                result.Success = true;
                _spamFilter.UpdateAlertSentList(alertMessage);
                _logger.Info(result.Message);
            }
            catch (Exception e)
            {
                result.Message = "Email with subject " + alertMessage.Subject + " failed to send . Error was :" + e.Message.ToString().ToString();
                result.Success = false;
                _logger.Error(result.Message);
            }
            return result;
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
        public ResultObj Alert()
        {
            _awake = true;
            ResultObj result = new ResultObj();
            result.Message = "SERVICE : AlertMessageService.Alert() ";
            AlertServiceInitObj alertObj = new AlertServiceInitObj();
            result.Success = false;
            alertObj.IsAlertServiceReady = false;
            _rabbitRepo.Publish<AlertServiceInitObj>("alertServiceReady", alertObj);
            _logger.Info("Published event AlertServiceItitObj.IsAlertServiceReady = false");
            Stopwatch timerInner = new Stopwatch();
            timerInner.Start();
            try
            {
                result.Message += InitAlerts(_userInfos);
                int count = SendAlerts();
                result.Message += "Success : AlertMessageService.Alert Executed ";
                if (_alert)
                {
                    result.Message += "Info : Message sent was to :" + count + " users";
                }
                result.Success = true;
                _logger.Info(result.Message);
            }
            catch (Exception e)
            {
                result.Message += "Error : AlertMessageService.Alert Execute failed : Error was : " + e.ToString();
                result.Success = false;
                _logger.Error(result.Message);
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
                        _logger.Info("Sleeping for " + (10000 - timeTakenInnerInt) + " ms to allow message to pass to scheduler");
                        new System.Threading.ManualResetEvent(false).WaitOne(10000 - timeTakenInnerInt);
                    }
                    alertObj.IsAlertServiceReady = true;
                    _rabbitRepo.Publish<AlertServiceInitObj>("alertServiceReady", alertObj);
                    _logger.Info("Published event AlertServiceItitObj.IsAlertServiceReady = true");
                }
                catch (Exception e)
                {
                    _logger.Error("Error : Can not publish event  AlertServiceItitObj.IsAlertServiceReady " + e.Message.ToString().ToString());
                }
                _awake = false;
            }
            return result;
        }
        public String InitAlerts(List<UserInfo> userInfos)
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
                if (monitorStatusAlert.AlertSent == false && !noAlertSentStored) publishAlertSentList.Add(monitorStatusAlert);
                string userId = monitorStatusAlert.UserID;
                UserInfo userInfo = userInfos.FirstOrDefault(u => u.UserID == userId);
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
                _logger.Warn("Warning republishing AlertSent List check coms. ");
                PublishAlertsRepo.ProcessorAlertSent(_logger, _rabbitRepo, publishAlertSentList, _processorList);
            }
            CheckAlerts(updateAlertFlagList);
            if (updateAlertFlagList.Count() > 0)
            {
                _alert = true;
                PublishAlertsRepo.ProcessorAlertFlag(_logger, _rabbitRepo, updateAlertFlagList, _processorList);
            }
            else _alert = false;
            return resultStr;
        }
        private void CheckAlerts(List<MonitorStatusAlert> updateAlertFlagList)
        {
            if (updateAlertFlagList == null || updateAlertFlagList.Count() == 0) return;
            if (!_checkAlerts) return;
            var pingParams = new PingParams() { IsAdmin = false };
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
            _logger.Info(" Checking " + monitorPingInfos.Count() + " Alerts ");
            var connectFactory = new ConnectFactory(_config,false);
            var netConnectCollection = new NetConnectCollection(_logger,_config,connectFactory);
            SemaphoreSlim semaphore = new SemaphoreSlim(1);
            netConnectCollection.NetConnectFactory(monitorPingInfos, pingParams,true,false,semaphore);
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
            monitorPingInfos.Where(w => w.MonitorStatus.IsUp).ToList().ForEach(m =>
           {
               updateAlertFlagList.RemoveAll(r => r.ID == m.MonitorIPID);
               _logger.Warn(" Warning : Overturned Alert with MonitorPingID = " + m.MonitorIPID + " . On Processor with AppID " + m.AppID + " . ");
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
            PublishAlertsRepo.ProcessorResetAlerts(_logger, _rabbitRepo, monitorIPDic);
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
        public int SendAlerts()
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
                        if (Send(alertMessage).Success)
                        {
                            UpdateAndPublishAlertSentList(alertMessage, publishAlertSentList);
                        }
                        count++;
                    }
                    else
                    {
                        UpdateAndPublishAlertSentList(alertMessage, publishAlertSentList);
                    }
                }
                PublishAlertsRepo.ProcessorAlertSent(_logger, _rabbitRepo, publishAlertSentList, _processorList);
            }
            return count;
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
        public ResultObj UpdateUserInfo(UserInfo userInfo)
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
                    FileRepo.SaveStateJsonZ<List<UserInfo>>("UserInfos", _userInfos);
                    _logger.Info("Saved UserInfos to file statestore ");
                }
                catch (Exception e)
                {
                    _logger.Error("Error : Can not save UserInfos to statestore Error was : " + e.Message.ToString());
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
