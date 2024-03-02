using NetworkMonitor.Objects;
using NetworkMonitor.Objects.ServiceMessage;
using NetworkMonitor.Objects.Repository;
using NetworkMonitor.Connection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace NetworkMonitor.Alert.Services;
public class AlertProcessor
{

    private IRabbitRepo _rabbitRepo;
    private ILogger _logger;
    private List<UserInfo> _userInfos = new List<UserInfo>();
    private IEmailProcessor _emailProcessor;
    private IProcessorState _processorState;
    private INetConnectCollection _netConnectCollection;
    private AlertParams _alertParams;


    private IAlertProcess _monitorAlertProcess = new AlertProcess() { PublishProcessor = true, CheckAlerts = true, };
    private IAlertProcess _predictAlertProcess = new AlertProcess() { PublishPredict = true, CheckAlerts = false };

    public IAlertProcess MonitorAlertProcess { get => _monitorAlertProcess; set => _monitorAlertProcess = value; }
    public IAlertProcess PredictAlertProcess { get => _predictAlertProcess; set => _predictAlertProcess = value; }

    public AlertProcessor(ILogger logger, IRabbitRepo rabbitRepo, IEmailProcessor emailProcessor, IProcessorState processorState, INetConnectCollection netConnectCollection, AlertParams alertParmas, List<UserInfo> userInfos)
    {
        _rabbitRepo = rabbitRepo;
        _logger = logger;
        _emailProcessor = emailProcessor;
        _processorState = processorState;
        _netConnectCollection = netConnectCollection;
        _alertParams = alertParmas;
        _monitorAlertProcess.AlertThreshold = alertParmas.AlertThreshold;
        _predictAlertProcess.AlertThreshold = alertParmas.PredictThreshold;
        _monitorAlertProcess.CheckAlerts = alertParmas.CheckAlerts;
        _monitorAlertProcess.DisableEmailAlert = alertParmas.DisableMonitorEmailAlert;
        _predictAlertProcess.DisableEmailAlert = alertParmas.DisablePredictEmailAlert;

        _userInfos = userInfos;



    }
    public List<MonitorStatusAlert> MonitorAlerts
    {
        get
        {
            // Use OfType<MonitorStatusAlert>() to filter and safely cast to non-nullable MonitorStatusAlert
            return _monitorAlertProcess.Alerts
                .OfType<MonitorStatusAlert>()
                .ToList();
        }
        set
        {
            // Cast each MonitorStatusAlert to IAlertable and assign the list
            _monitorAlertProcess.Alerts = value.Cast<IAlertable>().ToList();
        }
    }

    public List<PredictStatusAlert> PredictAlerts
    {
        get
        {
            // Use OfType<PredictStatusAlert>() to filter and safely cast to non-nullable PredictStatusAlert
            return _predictAlertProcess.Alerts
                .OfType<PredictStatusAlert>()
                .ToList();
        }
        set
        {
            // Cast each PredictStatusAlert to IAlertable and assign the list
            _predictAlertProcess.Alerts = value.Cast<IAlertable>().ToList();
        }
    }


    public async Task<ResultObj> MonitorAlert()
    {
        return await Alert(MonitorAlertProcess);
    }
    public async Task<ResultObj> PredictAlert()
    {
        return await Alert(PredictAlertProcess);
    }
    public async Task<ResultObj> Alert(IAlertProcess alertProcess)
    {
        alertProcess.Awake = true;
        AlertServiceInitObj alertObj = new AlertServiceInitObj();
        ResultObj result = new ResultObj();
        result.Message = $"SERVICE : AlertProcessor.Alert({alertProcess.PublishPrefix}) ";
        result.Success = false;
        if (alertProcess.PublishScheduler)
        {
            alertObj = new AlertServiceInitObj();
            alertObj.IsAlertServiceReady = false;
            await _rabbitRepo.PublishAsync<AlertServiceInitObj>("alertServiceReady", alertObj);
            _logger.LogInformation("Published event AlertServiceItitObj.IsAlertServiceReady = false");

        }
        Stopwatch timerInner = new Stopwatch();
        timerInner.Start();
        try
        {
            result.Message += await InitAlerts(_userInfos, alertProcess);
            int count = await SendAlerts(alertProcess);
            result.Message += "Success : AlertMessageService.Alert Executed ";
            if (alertProcess.Alert)
            {
                result.Message += "Info : Message sent was to :" + count + " users";
            }
            result.Success = true;
            result.Data = count;
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
                if (alertProcess.PublishScheduler)
                {
                    alertObj.IsAlertServiceReady = true;
                    await _rabbitRepo.PublishAsync<AlertServiceInitObj>("alertServiceReady", alertObj);
                    _logger.LogInformation("Published event AlertServiceItitObj.IsAlertServiceReady = true");

                }
            }
            catch (Exception e)
            {
                _logger.LogError("Error : Can not publish event  AlertServiceItitObj.IsAlertServiceReady " + e.Message.ToString().ToString());
            }
            alertProcess.Awake = false;
        }
        return result;
    }
    public async Task<String> InitAlerts(List<UserInfo> userInfos, IAlertProcess alertProcess)
    {
        string resultStr = " InitAlerts : ";
        alertProcess.Alert = false;
        alertProcess.AlertMessages = new List<AlertMessage>();
        var updateAlertFlagList = new List<IAlertable>();
        var publishAlertSentList = new List<IAlertable>();
        while (alertProcess.IsAlertRunning)
        {
            resultStr += " Warning : Waiting for Alert to stop running ";
            new System.Threading.ManualResetEvent(false).WaitOne(1000);
        }
        alertProcess.IsAlertRunning = true;
        List<IAlertable> statusAlerts;
        if (alertProcess.IsMonitorProcess)
        {
            statusAlerts = alertProcess.Alerts.Where(a => a is MonitorStatusAlert)
                                               .Select(a => new MonitorStatusAlert(a))
                                               .ToList<IAlertable>();
        }
        else if (alertProcess.IsPredictProcess)
        {
            statusAlerts = alertProcess.Alerts.Where(a => a is PredictStatusAlert)
                                               .Select(a => new PredictStatusAlert(a))
                                               .ToList<IAlertable>();
        }
        else
        {
            // Handle other types of alerts if necessary
            statusAlerts = new List<IAlertable>();
        }
        alertProcess.IsAlertRunning = false;

        foreach (IAlertable statusAlert in statusAlerts)
        {

            // If the previoud publish of AlertSent failed then need to republish.
            bool noAlertSentStored = alertProcess.UpdateAlertSentList.FirstOrDefault(w => w.ID == statusAlert.ID) == null;
            if (statusAlert.AlertFlag = true && statusAlert.AlertSent == false && !noAlertSentStored) publishAlertSentList.Add(statusAlert);
            string? userId = statusAlert.UserID;
            var testUserInfo = userInfos.FirstOrDefault(u => u.UserID == userId);
            if (testUserInfo == null)
            {
                _logger.LogWarning(" Warning : StatusAlert contains userId not present in UserInfos State store  .");
                continue;
            }
            UserInfo userInfo = new UserInfo(testUserInfo);

            bool disableEmail = !_emailProcessor.VerifyEmail(userInfo, statusAlert);
            if (userInfo.UserID == "default")
            {
                if (userInfo.Email == null) userInfo.Email = "missing@email";
                userInfo.Name = userInfo.Email.Split('@')[0];
                userId = userInfo.Email;
                userInfo.UserID = userId;
                userInfo.MonitorAlertEnabled = true;
                userInfo.PredictAlertEnabled = false;
            }

            if (disableEmail) userInfo.DisableEmail = true;

            if (statusAlert.AddUserEmail == "delete") userInfo.DisableEmail = true;

            statusAlert.UserName = userInfo.Name;
            if (statusAlert.DownCount > alertProcess.AlertThreshold && statusAlert.AlertSent == false && noAlertSentStored)
            {
                // Its not the first messge for this user so we need to add a new line
                if (alertProcess.AlertMessages.FirstOrDefault(a => a.UserID == userId) != null)
                {
                    var alertMessage = alertProcess.AlertMessages.FirstOrDefault(a => a.UserID == userId);
                    if (alertMessage == null)
                    {
                        _logger.LogWarning($" Warning : No alert messages contains userId {userId} .");
                        continue;
                    }
                    alertMessage.Message += "\n" + statusAlert.EndPointType!.ToUpper() + " Alert for host at address " + statusAlert.Address + " status message is " + statusAlert.Message + " . " +
                                               "\nHost down count is " + statusAlert.DownCount + "\nThe time of this event is  " + statusAlert.EventTime + "\n" +
                                               " The Processing server ID was " + statusAlert.AppID + " The timeout was set to " + statusAlert.Timeout + " ms. \n\n";
                    alertMessage.AlertFlagObjs.Add(statusAlert);
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
                        alertMessage.Message = "Alert message for " + statusAlert.UserName + " . ";
                        alertMessage.Message += "\n" + statusAlert.EndPointType!.ToUpper() + " Alert for host at address " + statusAlert.Address + " status message is " + statusAlert.Message + " . " +
                                  "\nNumber of events " + statusAlert.DownCount + "\nThe time of latest event is  " + statusAlert.EventTime + "\n" +
                                  " The Agents ID processing the host was " + statusAlert.AppID + " The timeout was set to " + statusAlert.Timeout + " ms. \n\n";
                        if (statusAlert is PredictStatusAlert)
                        {
                            alertMessage.dontSend = userInfo.DisableEmail || !userInfo.PredictAlertEnabled;
                        }
                        else if (statusAlert is MonitorStatusAlert)
                        {
                            alertMessage.dontSend = userInfo.DisableEmail || !userInfo.MonitorAlertEnabled;
                        }

                        alertMessage.AlertFlagObjs.Add(statusAlert);
                        alertProcess.AlertMessages.Add(alertMessage);
                    }
                }
                updateAlertFlagList.Add(statusAlert);
                //statusAlert.AlertFlag = true;
            }
        }
        if (publishAlertSentList.Count() != 0)
        {
            _logger.LogWarning("Warning republishing AlertSent List check coms. ");
            if (alertProcess.PublishProcessor) await PublishAlertsRepo.ProcessorAlertSent(_logger, _rabbitRepo, publishAlertSentList, _processorState.EnabledProcessorList);
        }
        if (alertProcess.CheckAlerts) await CheckAlerts(updateAlertFlagList, alertProcess);

        if (updateAlertFlagList.Count() > 0)
        {
            alertProcess.Alert = true;
            if (alertProcess.PublishProcessor) await PublishAlertsRepo.ProcessorAlertFlag(_logger, _rabbitRepo, updateAlertFlagList, _processorState.EnabledProcessorList);
        }
        else alertProcess.Alert = false;
        return resultStr;
    }
    public async Task<int> SendAlerts(IAlertProcess alertProcess)
    {
        int count = 0;
        if (alertProcess.Alert)
        {
            var publishAlertSentList = new List<IAlertable>();
            foreach (AlertMessage alertMessage in alertProcess.AlertMessages)
            {
                alertMessage.SendTrustPilot = _emailProcessor.SendTrustPilot;
                alertMessage.Subject = "Network Monitor Alert!";
                if (!alertMessage.dontSend)
                {
                    if (alertProcess.DisableEmailAlert)
                    {
                        alertMessage.UserInfo.Email = "support@mahadeva.co.uk";
                    }

                    alertMessage.VerifyLink = false;
                    var result = new ResultObj();
                    result = await _emailProcessor.SendAlert(alertMessage);

                    if (result.Success)
                    {
                        result.Message += " Success : Sent alert message to " + alertMessage.EmailTo;
                        UpdateAndPublishAlertSentList(alertMessage, publishAlertSentList, alertProcess);
                        _logger.LogInformation(result.Message);
                        count++;
                    }
                    else
                    {
                        _logger.LogError(result.Message);
                    }


                }
                else
                {
                    UpdateAndPublishAlertSentList(alertMessage, publishAlertSentList, alertProcess);
                }
            }
            await PublishAlertsRepo.ProcessorAlertSent(_logger, _rabbitRepo, publishAlertSentList, _processorState.EnabledProcessorList);
        }
        return count;
    }


    private void UpdateAndPublishAlertSentList(AlertMessage alertMessage, List<IAlertable> publishAlertSentList, IAlertProcess alertProcess)
    {
        foreach (IAlertable alertFlagObj in alertMessage.AlertFlagObjs)
        {
            var updateAlert = alertProcess.Alerts.Where(w => w.ID == alertFlagObj.ID).FirstOrDefault();
            if (updateAlert != null)
            {
                updateAlert.AlertSent = true;
                publishAlertSentList.Add(alertFlagObj);
                alertProcess.UpdateAlertSentList.Add(alertFlagObj);
            }
            else
            {
                _logger.LogError($" Error : can not find Alert with ID {alertFlagObj.ID} It is present in AlertMessages but not in Alerts. This should not be possible.");
            }
            //var updateMonitorStatusAlert = _monitorStatusAlerts.FirstOrDefault(w => w.ID == alertFlagObj.ID);
            //if (updateMonitorStatusAlert != null) updateMonitorStatusAlert.AlertSent = true;
        }
    }

    public List<ResultObj> ResetMonitorAlerts(List<AlertFlagObj> alertFlagObjs)
    {
        return ResetAlerts(alertFlagObjs, _monitorAlertProcess);
    }
    public List<ResultObj> ResetPredictAlerts(List<AlertFlagObj> alertFlagObjs)
    {
        return ResetAlerts(alertFlagObjs, _predictAlertProcess);
    }
    private List<ResultObj> ResetAlerts(List<AlertFlagObj> alertFlagObjs, IAlertProcess alertProcess)
    {
        var results = new List<ResultObj>();
        var result = new ResultObj();
        alertFlagObjs.ForEach(f =>
        {
            try
            {
                while (alertProcess.IsAlertRunning)
                {
                    result.Message += " Info : Waiting for Alert to stop running ";
                    new System.Threading.ManualResetEvent(false).WaitOne(5000);
                }
                var updateStatusAlerts = alertProcess.Alerts.Where(w => w.ID == f.ID).ToList();
                if (updateStatusAlerts == null)
                {
                    result.Success = false;
                    result.Message += " Warning : Unable to find any MonitorStatusAlerts with ID " + f.ID;
                }
                else
                {
                    foreach (var updateStatusAlert in updateStatusAlerts)
                    {
                        updateStatusAlert.AlertFlag = false;
                        updateStatusAlert.AlertSent = false;
                        updateStatusAlert.DownCount = 0;
                        result.Success = true;
                        result.Message += " Success : updated MonitorStatusAlert with ID " + f.ID + " with AppID " + f.AppID + " . ";

                    }
                    alertProcess.UpdateAlertSentList.RemoveAll(r => r.ID == f.ID);
                }

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
    private async Task CheckAlerts(List<IAlertable> updateAlertFlagList, IAlertProcess alertProcess)
    {
        if (updateAlertFlagList == null || updateAlertFlagList.Count() == 0) return;
        var pingParams = new PingParams();
        var monitorPingInfos = new List<MonitorPingInfo>();
        int maxTimeout = 0;
        // exclude MonitorPingInfos that have EndPointType set to string values in ExcludeEndPointTypList
        var excludeEndPoints = new ExcludeEndPointTypeList();
        updateAlertFlagList.Where(w => !excludeEndPoints.Contains(w.EndPointType!)).ToList().ForEach(a =>
        {
            monitorPingInfos.Add(new MonitorPingInfo()
            {
                ID = a.ID,
                MonitorIPID = a.ID,
                Address = a.Address!,
                AppID = a.AppID,
                EndPointType = a.EndPointType!,
                Timeout = a.Timeout,
                Enabled = true
            });
            if (a.Timeout > maxTimeout) maxTimeout = a.Timeout;
        });
        _logger.LogInformation(" Checking " + monitorPingInfos.Count() + " Alerts ");
        SemaphoreSlim semaphore = new SemaphoreSlim(1);
        _netConnectCollection.NetConnectFactory(monitorPingInfos, pingParams, true, false, semaphore).Wait();
        var netConnects = _netConnectCollection.GetNonLongRunningNetConnects().ToList();
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
           alertProcess.AlertMessages.ForEach(a =>
           {
               a.AlertFlagObjs.RemoveAll(r => r.ID == m.MonitorIPID);
           });
           if (!monitorIPDic.ContainsKey(m.AppID!))
           {
               monitorIPDic.Add(m.AppID!, new List<int>() { m.MonitorIPID });
           }
           else
           {
               monitorIPDic[m.AppID!].Add(m.MonitorIPID);
           }
       });
        alertProcess.AlertMessages.RemoveAll(r => r.AlertFlagObjs.Count() == 0);
        await PublishAlertsRepo.ProcessorResetAlerts(_logger, _rabbitRepo, monitorIPDic);
    }


}