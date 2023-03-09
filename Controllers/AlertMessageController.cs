using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NetworkMonitor.Objects;
using NetworkMonitor.Objects.ServiceMessage;
using NetworkMonitor.Service.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using NetworkMonitor.Utils;
using System.Threading.Tasks;
using Dapr;
namespace NetworkMonitor.Service.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AlertMessageController : ControllerBase
    {
        private readonly ILogger<AlertMessageController> _logger;
        private IAlertMessageService _alertMessageService;
        private IDataQueueService _dataQueueService;
        public AlertMessageController(ILogger<AlertMessageController> logger, IAlertMessageService alertMessageService, IDataQueueService dataQueueService)
        {
            _logger = logger;
            _alertMessageService = alertMessageService;
            _dataQueueService=dataQueueService;
        }
        [Topic("pubsub", "serviceWakeUp")]
        [HttpPost("wakeup")]
        public ActionResult<ResultObj> WakeUp()
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : WakeUp : ";
            try
            {
                _alertMessageService.Awake=true;
                result.Message+="Success : Set Awake to true in AlertMessageService.";
                result.Success=true;
                //result=_alertMessageService.WakeUp();
                _logger.LogWarning(result.Message);
            }
            catch (Exception e)
            {
                result.Data = null;
                result.Success = false;
                result.Message += "Error : Failed to receive message : Error was : " + e.Message + " ";
                _logger.LogError(result.Message);
            }
            return result;
        }
        [Topic("pubsub", "alertMessageInit")]
        [HttpPost("AlertMessageInit")]
        [Consumes("application/json")]
        public ActionResult<ResultObj> AlertMessageInit([FromBody] AlertServiceInitObj initObj)
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : AlertMessageInit : ";
            try
            {
                _alertMessageService.init(initObj);
                result.Message += "Success ran ok ";
                result.Success = true;
                _logger.LogInformation(result.Message);
            }
            catch (Exception e)
            {
                result.Data = null;
                result.Success = false;
                result.Message += "Error : Failed to receive message : Error was : " + e.Message + " ";
                _logger.LogError(result.Message);
            }
            return result;
        }
        [Topic("pubsub", "alertMessageResetAlerts")]
        [HttpPost("AlertMessageResetAlerts")]
        public ActionResult<ResultObj> AlertMessageResetAlerts(List<AlertFlagObj> alertFlagObjs)
        {
              ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : AlertMessageResetAlerts : ";
            try
            {
                var results= _alertMessageService.ResetAlerts(alertFlagObjs);
                results.ForEach(f => result.Message+=f.Message);
                result.Success= results.All(a => a.Success==true) && results.Count()!=0;
                result.Data =results;
                _logger.LogInformation(result.Message);
            }
            catch (Exception e)
            {
                result.Data = null;
                result.Success = false;
                result.Message += "Error : Failed to receive message : Error was : " + e.Message + " ";
                _logger.LogError(result.Message);
            }
            return result;
        }
        [Topic("pubsub", "alertMessage")]
        [HttpPost("AlertMessage")]
        public ActionResult<ResultObj> AlertMessage(AlertMessage alertMessage)
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : AlertMessage : ";
            try
            {
                result = _alertMessageService.Send(alertMessage);
                _logger.LogInformation(result.Message);
            }
            catch (Exception e)
            {
                result.Data = null;
                result.Success = false;
                result.Message += "Error : Failed to run AlertMessage : Error was : " + e.Message + " ";
                _logger.LogError("Error : Failed to run AlertMessage : Error was : " + e.Message + " ");
            }
            return result;
        }
        [Topic("pubsub", "updateUserInfoAlertMessage")]
        [HttpPost("UpdateUserInfoAlertMessage")]
        public ActionResult<ResultObj> UpdateUserInfoAlertMessage(UserInfo userInfo)
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : UpdateUserInfoAlertMessage : ";
            try
            {
                result = _alertMessageService.UpdateUserInfo(userInfo);
                _logger.LogInformation(result.Message);
            }
            catch (Exception e)
            {
                result.Data = null;
                result.Success = false;
                result.Message += "Error : Failed to run UpdateUserInfoAlertMessage : Error was : " + e.Message + " ";
                _logger.LogError("Error : Failed to run UpdateUserInfoAlertMessage : Error was : " + e.Message + " ");
            }
            return result;
        }
        [Topic("pubsub", "monitorAlert")]
        [HttpPost("MonitorAlert")]
        public ActionResult<ResultObj> MonitorAlert()
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : MonitorAlert : ";
            try
            {
                result = _alertMessageService.Alert();
                _logger.LogInformation(result.Message);
            }
            catch (Exception e)
            {
                result.Data = null;
                result.Success = false;
                result.Message += "Error : Failed to run MonitorAlert : Error was : " + e.Message + " ";
                _logger.LogError("Error : Failed to run MonitorAlert : Error was : " + e.Message + " ");
            }
            return result;
        }
        [Topic("pubsub", "alertUpdateMonitorStatusAlerts")]
        [HttpPost("AlertUpdateMonitorStatusAlerts")]
        [Consumes("application/json")]
        public async Task<ResultObj> AlertUpdateMonitorStatusAlerts([FromBody] byte[] monitorStatusAlertBytes)
        {
            var result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : alertUpdateMonitorStatusAlerts : ";
            try
            {
                while (_alertMessageService.IsAlertRunning)
                {
                    result.Message += "Info : Waiting for Alert to stop running ";
                    new System.Threading.ManualResetEvent(false).WaitOne(5000);
                }
                _alertMessageService.IsAlertRunning=true;
                await _dataQueueService.AddProcessorDataBytesToQueue(monitorStatusAlertBytes,_alertMessageService.MonitorStatusAlerts);
                _alertMessageService.IsAlertRunning=false;
                result.Message += "Success added task AlertMonitorStatusAlerts ";
                result.Success = true;
                result.Data = null;
                _logger.LogInformation(result.Message);
                _logger.LogDebug("AlertMonitorStatusAlerts : " + JsonUtils.writeJsonObjectToString(_alertMessageService.MonitorStatusAlerts));
            }
            catch (Exception e)
            {
                
                result.Success = false;
                result.Message += "Error : Failed to set AlertMonitorStatusAlerts : Error was : " + e.Message + " ";
                _logger.LogError("Error : Failed to set AlertMonitorStatusAlerts : Error was : " + e.Message + " ");
            }
            return result;
        }
    }
}
