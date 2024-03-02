
using NetworkMonitor.Alert.Services;
using NetworkMonitor.Objects;
using NetworkMonitor.Objects.Repository;
using NetworkMonitor.Connection;
using NetworkMonitor.Objects.ServiceMessage;
using System;
using System.Collections.Generic;

namespace NetworkMonitor.Alert.Tests;
public class AlertTestData
{

    public static SystemParams GetSystemParams()
    {
        var systemParams = new SystemParams
        {
            EmailEncryptKey = "testkey",
            // Set other properties as needed
        };
        return systemParams;
    }
    public static List<ProcessorObj> GetProcesorList()
    {
        var processorList = new List<ProcessorObj>();
        processorList.Add(new ProcessorObj() { AppID = "test" });
        return processorList;
    }
    public static List<IAlertable> GetMonitorAlerts()
    {
        var alerts = new List<IAlertable>();
        alerts.Add(new MonitorStatusAlert()
        {
            ID = 1,
            UserID = "test",
            Address = "1.1.1.1",
            UserName = "",
            AppID = "test",
            EndPointType = "icmp",
            Timeout = 10000,
            AddUserEmail = "support@mahadeva.co.uk",
            // Assuming these properties come from StatusObj and are relevant
            AlertFlag = false,
            AlertSent = false,
            DownCount = 0,
            EventTime = DateTime.UtcNow,
            IsUp = false,
            Message = "Timeout",
            MonitorPingInfoID = 1,
        });
        alerts.Add(new MonitorStatusAlert()
        {
            ID = 2,
            UserID = "test",
            Address = "2.2.2.2",
            UserName = "",
            AppID = "test",
            EndPointType = "icmp",
            Timeout = 10000,
            // Assuming these properties come from StatusObj and are relevant
            AlertFlag = true,
            AlertSent = false,
            DownCount = 5,
            EventTime = DateTime.UtcNow,
            IsUp = false,
            Message = "Timeout",
            MonitorPingInfoID = 2,
        });
        alerts.Add(new MonitorStatusAlert()
        {
            ID = 3,
            UserID = "nonuser",
            Address = "2.2.2.2",
            UserName = "",
            AppID = "test",
            EndPointType = "icmp",
            Timeout = 10000,
            AddUserEmail = "support@mahadeva.co.uk",
            // Assuming these properties come from StatusObj and are relevant
            AlertFlag = false,
            AlertSent = false,
            DownCount = 5,
            EventTime = DateTime.UtcNow,
            IsUp = false,
            Message = "Timeout",
            MonitorPingInfoID = 3,
        });
        alerts.Add(new MonitorStatusAlert()
        {
            ID = 4,
            UserID = "default",
            Address = "2.2.2.2",
            UserName = "",
            AppID = "test",
            EndPointType = "icmp",
            Timeout = 10000,
            AddUserEmail = "bademail@bademail",
            // Assuming these properties come from StatusObj and are relevant
            AlertFlag = true,
            AlertSent = false,
            DownCount = 5,
            EventTime = DateTime.UtcNow,
            IsUp = false,
            Message = "Timeout",
            MonitorPingInfoID = 4,
        });
          alerts.Add(new MonitorStatusAlert()
        {
            ID = 5,
            UserID = "testdisableemail",
            Address = "2.2.2.2",
            UserName = "",
            AppID = "test",
            EndPointType = "icmp",
            Timeout = 10000,
            AddUserEmail = "bademail@bademail",
            // Assuming these properties come from StatusObj and are relevant
            AlertFlag = true,
            AlertSent = false,
            DownCount = 5,
            EventTime = DateTime.UtcNow,
            IsUp = false,
            Message = "Timeout",
            MonitorPingInfoID = 4,
        });
          alerts.Add(new MonitorStatusAlert()
        {
            ID = 6,
            UserID = "testdisablemonitor",
            Address = "2.2.2.2",
            UserName = "",
            AppID = "test",
            EndPointType = "icmp",
            Timeout = 10000,
            AddUserEmail = "contact@mahadeva.co.uk",
            // Assuming these properties come from StatusObj and are relevant
            AlertFlag = true,
            AlertSent = false,
            DownCount = 5,
            EventTime = DateTime.UtcNow,
            IsUp = false,
            Message = "Timeout",
            MonitorPingInfoID = 4,
        });
        return alerts;
    }

    public static DetectionResult GetDetectionResult(bool flag)
    {
        return new DetectionResult()
        {
            IsIssueDetected = flag
        };
    }
    public static List<IAlertable> GetPredictAlerts()
    {
        var alerts = new List<IAlertable>();
        alerts.Add(new PredictStatusAlert()
        {
            ID = 1,
            UserID = "test",
            Address = "1.1.1.1",
            UserName = "",
            AppID = "test",
            EndPointType = "icmp",
            Timeout = 10000,
            AddUserEmail = "support@mahadeva.co.uk",
            // Assuming these properties come from StatusObj and are relevant
            AlertFlag = false,
            AlertSent = false,
            ChangeDetectionResult = GetDetectionResult(false),
            SpikeDetectionResult = GetDetectionResult(false),
            EventTime = DateTime.UtcNow,

            Message = "Timeout",
            MonitorPingInfoID = 1,
        });
        alerts.Add(new PredictStatusAlert()
        {
            ID = 2,
            UserID = "test",
            Address = "2.2.2.2",
            UserName = "",
            AppID = "test",
            EndPointType = "icmp",
            Timeout = 10000,
            // Assuming these properties come from StatusObj and are relevant
            AlertFlag = true,
            AlertSent = false,
            ChangeDetectionResult = GetDetectionResult(true),
            SpikeDetectionResult = GetDetectionResult(true),
            EventTime = DateTime.UtcNow,

            Message = "Timeout",
            MonitorPingInfoID = 2,
        });
        alerts.Add(new PredictStatusAlert()
        {
            ID = 3,
            UserID = "nonuser",
            Address = "2.2.2.2",
            UserName = "",
            AppID = "test",
            EndPointType = "icmp",
            Timeout = 10000,
            AddUserEmail = "support@mahadeva.co.uk",
            // Assuming these properties come from StatusObj and are relevant
            AlertFlag = false,
            AlertSent = false,
            ChangeDetectionResult = GetDetectionResult(true),
            SpikeDetectionResult = GetDetectionResult(true),
            EventTime = DateTime.UtcNow,
            Message = "Timeout",
            MonitorPingInfoID = 3,
        });
        alerts.Add(new PredictStatusAlert()
        {
            ID = 4,
            UserID = "default",
            Address = "2.2.2.2",
            UserName = "",
            AppID = "test",
            EndPointType = "icmp",
            Timeout = 10000,
            AddUserEmail = "bademail@bademail",
            // Assuming these properties come from StatusObj and are relevant
            AlertFlag = true,
            AlertSent = false,

            EventTime = DateTime.UtcNow,
            ChangeDetectionResult = GetDetectionResult(true),
            SpikeDetectionResult = GetDetectionResult(true),
            Message = "Timeout",
            MonitorPingInfoID = 4,
        });
        alerts.Add(new PredictStatusAlert()
        {
            ID = 5,
            UserID = "testdisablepredict",
            Address = "2.2.2.2",
            UserName = "",
            AppID = "test",
            EndPointType = "icmp",
            Timeout = 10000,
            AddUserEmail = "contact@mahadeva.co.uk",
            // Assuming these properties come from StatusObj and are relevant
            AlertFlag = true,
            AlertSent = false,

            EventTime = DateTime.UtcNow,
            ChangeDetectionResult = GetDetectionResult(true),
            SpikeDetectionResult = GetDetectionResult(true),
            Message = "Timeout",
            MonitorPingInfoID = 4,
        });
        return alerts;
    }

    public static void AddDataPredictAlerts(List<IAlertable> alerts)
    {
        alerts.Add(new PredictStatusAlert()
        {
            ID = 4,
            UserID = "default",
            Address = "2.2.2.2",
            UserName = "",
            AppID = "badappid",
            EndPointType = "icmp",
            Timeout = 10000,
            AddUserEmail = "bademail@bademail",
            // Assuming these properties come from StatusObj and are relevant
            AlertFlag = true,
            AlertSent = false,

            EventTime = DateTime.UtcNow,
            ChangeDetectionResult = GetDetectionResult(true),
            SpikeDetectionResult = GetDetectionResult(true),
            Message = "Timeout",
            MonitorPingInfoID = 5,
        });
    }
    public static AlertParams GetAlertParams()
    {
        return new AlertParams()
        {
            CheckAlerts = true,
            DisableEmails = false,
             DisablePredictEmailAlert = true,
              DisableMonitorEmailAlert = true,
            AlertThreshold = 4,
            PredictThreshold = 0
        };
    }
    public static AlertProcess GetMonitorAlertProcess()
    {
        var monitorAlertProcess = new AlertProcess();
        monitorAlertProcess.Alerts = GetMonitorAlerts();
        monitorAlertProcess.AlertThreshold = 2;
        return monitorAlertProcess;
    }

    public static AlertProcess GetPredictAlertProcess()
    {
        var predictAlertProcess = new AlertProcess();
        predictAlertProcess.Alerts = GetPredictAlerts();
        predictAlertProcess.AlertThreshold = 2;
        return predictAlertProcess;
    }

    public static List<UserInfo> GetUserInfos()
    {
        var userInfos = new List<UserInfo>();
        userInfos.Add(new UserInfo()
        {
            UserID = "test",
            Email = "support@mahadeva.co.uk",
            Email_verified = true,
            DisableEmail = false,
            PredictAlertEnabled=true,
            MonitorAlertEnabled=true,
            Name = "test user"

        });
        userInfos.Add(new UserInfo()
        {
            UserID = "default",
            Email = "bademailtest",
            Email_verified = true,
            DisableEmail = false,
            PredictAlertEnabled=true,
            MonitorAlertEnabled=true,
            Name = "default"

        });
           userInfos.Add(new UserInfo()
        {
            UserID = "testdisableemail",
            Email = "support@mahadeva.co.uk",
            Email_verified = true,
            DisableEmail = true,
            Name = "test user"

        });
        userInfos.Add(new UserInfo()
        {
            UserID = "testdisablepredict",
            Email = "support@mahadeva.co.uk",
            Email_verified = true,
            DisableEmail = false,
            PredictAlertEnabled=false,
            Name = "default"

        });
         userInfos.Add(new UserInfo()
        {
            UserID = "testdisablemonitor",
            Email = "support@mahadeva.co.uk",
            Email_verified = true,
            DisableEmail = false,
            MonitorAlertEnabled=false,
            Name = "default"

        });
        return userInfos;
    }

}