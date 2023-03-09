using NetworkMonitor.Objects.ServiceMessage;
using NetworkMonitor.Objects;
using System.Collections.Generic;

namespace NetworkMonitor.Service.Services
{
    public interface IAlertMessageService
    {
        public void init(AlertServiceInitObj alertObj);

        List<MonitorStatusAlert> MonitorStatusAlerts{get;set;}
        List<ResultObj> ResetAlerts(List<AlertFlagObj> alertFlagObjs);

                //ResultObj QueueRemoveFromAlertSentList(AlertFlagObj alertFlagObj);
        ResultObj UpdateUserInfo(UserInfo userInfo);
        ResultObj WakeUp();

        bool IsAlertRunning{get;set;}
        bool Awake{get;set;}

           public ResultObj Alert();
        ResultObj Send(AlertMessage alertMessage);
       
      
      
    }
}