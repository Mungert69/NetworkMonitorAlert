
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
    public static List<ProcessorObj> GetProcesorList()
    {
        var processorList = new List<ProcessorObj>();
        processorList.Add(new ProcessorObj() { AppID = "test" });
        return processorList;
    }
    public static List<IAlertable> GetAlerts()
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
        return alerts;
    }
    public static AlertParams GetAlertParams()
    {
        return new AlertParams()
        {
            CheckAlerts = true,
            DisableEmailAlert = true,
            AlertThreshold = 4,
            PredictThreshold = 0
        };
    }
    public static AlertProcess GetMonitorAlertProcess()
    {
        var monitorAlertProcess = new AlertProcess();
        monitorAlertProcess.Alerts = GetAlerts();
        monitorAlertProcess.AlertThreshold = 2;
        return monitorAlertProcess;
    }

    public static List<UserInfo> GetUserInfos() { 
        var userInfos=new List<UserInfo>();
        userInfos.Add(new UserInfo() {
            UserID = "test",
            Email="support@mahadeva.co.uk",
            Email_verified=true,
            DisableEmail=false,
            Name="test user"

        });
        userInfos.Add(new UserInfo() {
            UserID = "default",
            Email="support@mahadeva.co.uk",
            Email_verified=true,
            DisableEmail=false,
            Name="default"

        });
        return userInfos;
    }

}