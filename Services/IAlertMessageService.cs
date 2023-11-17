using NetworkMonitor.Objects.ServiceMessage;
using NetworkMonitor.Objects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NetworkMonitor.Alert.Services
{
    public interface IAlertMessageService
    {
        public void InitService(AlertServiceInitObj alertObj);
          public Task Init();

        List<MonitorStatusAlert> MonitorStatusAlerts{get;set;}
        List<ResultObj> ResetAlerts(List<AlertFlagObj> alertFlagObjs);

                //ResultObj QueueRemoveFromAlertSentList(AlertFlagObj alertFlagObj);
        Task<ResultObj> UpdateUserInfo(UserInfo userInfo);
        ResultObj WakeUp();

        bool IsAlertRunning{get;set;}
        bool Awake{get;set;}

        Task<ResultObj> Alert();
        Task <ResultObj> Send(AlertMessage alertMessage);
        Task<List<ResultObj>> UserHostExpire(List<UserInfo> userInfos);
       
      
      
    }
}