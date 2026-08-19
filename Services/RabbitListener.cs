using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using NetworkMonitor.Objects.ServiceMessage;
using NetworkMonitor.Objects;
using NetworkMonitor.Alert.Services;
using System.Collections.Generic;
using System;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NetworkMonitor.Utils;
using NetworkMonitor.Utils.Helpers;
using NetworkMonitor.Objects.Repository;
using Microsoft.Extensions.Logging;
namespace NetworkMonitor.Alert.Services
{
    public interface IRabbitListener
    {
        Task<ResultObj> WakeUp();
        ResultObj AlertMessageInit(AlertServiceInitObj initObj);
        ResultObj AlertMessageResetAlerts(AlertServiceAlertObj alertServiceAlertObj);
        Task<ResultObj> AlertMessage(AlertMessage alertMessage);
        Task<ResultObj> UpdateUserInfoAlertMessage(UserInfo userInfo);
        Task<ResultObj> MonitorAlert();
        Task<ResultObj> AlertUpdateMonitorStatusAlerts(string monitorStatusAlertString);
        Task Shutdown();
        Task<ResultObj> Setup();
        Task<ResultObj> Setup(CancellationToken cancellationToken);
    }

    public class RabbitListener : RabbitListenerBase, IRabbitListener
    {
        private IAlertMessageService _alertMessageService;
        private IDataQueueService _dataQueueService;
        public RabbitListener(IAlertMessageService alertMessageService, IDataQueueService dataQueueService, ILogger<RabbitListenerBase> logger, SystemParams systemParams) : base(logger, DeriveSystemUrl(systemParams))
        {
            _alertMessageService = alertMessageService;
            _dataQueueService = dataQueueService;
        }



        private static SystemUrl DeriveSystemUrl(SystemParams systemParams)
        {
            return systemParams.ThisSystemUrl;
        }
        protected override void InitRabbitMQObjs()
        {
            _rabbitMQObjs.Add(new RabbitMQObj()
            {
                ExchangeName = "serviceWakeUp",
                FuncName = "serviceWakeUp",
                MessageTimeout = 60000
            });
            _rabbitMQObjs.Add(new RabbitMQObj()
            {
                ExchangeName = "alertMessageInit",
                FuncName = "alertMessageInit"
            });
            _rabbitMQObjs.Add(new RabbitMQObj()
            {
                ExchangeName = "alertMessageResetAlerts",
                FuncName = "alertMessageResetAlerts"
            });
            _rabbitMQObjs.Add(new RabbitMQObj()
            {
                ExchangeName = "alertMessageResetPredictAlerts",
                FuncName = "alertMessageResetPredictAlerts"
            });
            _rabbitMQObjs.Add(new RabbitMQObj()
            {
                ExchangeName = "alertMessage",
                FuncName = "alertMessage",
                MessageTimeout = 60000
            });
            _rabbitMQObjs.Add(new RabbitMQObj()
            {
                ExchangeName = "updateUserInfoAlertMessage",
                FuncName = "updateUserInfoAlertMessage"
            });
            _rabbitMQObjs.Add(new RabbitMQObj()
            {
                ExchangeName = "monitorAlert",
                FuncName = "monitorAlert",
                MessageTimeout = 60000
            });
            _rabbitMQObjs.Add(new RabbitMQObj()
            {
                ExchangeName = "predictAlert",
                FuncName = "predictAlert",
                MessageTimeout = 60000
            });
            _rabbitMQObjs.Add(new RabbitMQObj()
            {
                ExchangeName = "alertUpdateMonitorStatusAlerts",
                FuncName = "alertUpdateMonitorStatusAlerts",
                MessageTimeout = 60000
            });
            _rabbitMQObjs.Add(new RabbitMQObj()
            {
                ExchangeName = "alertUpdatePredictStatusAlerts",
                FuncName = "alertUpdatePredictStatusAlerts",
                MessageTimeout = 60000
            });
            _rabbitMQObjs.Add(new RabbitMQObj()
            {
                ExchangeName = "userHostExpire",
                FuncName = "userHostExpire",
                MessageTimeout = 86300000
            });
            _rabbitMQObjs.Add(new RabbitMQObj()
            {
                ExchangeName = "userProcessorExpire",
                FuncName = "userProcessorExpire",
                MessageTimeout = 86300000
            });
            _rabbitMQObjs.Add(new RabbitMQObj()
            {
                ExchangeName = "userUpgrade",
                FuncName = "userUpgrade",
                MessageTimeout = 86300000
            });
            _rabbitMQObjs.Add(new RabbitMQObj()
            {
                ExchangeName = "sendHostReport",
                FuncName = "sendHostReport",
                MessageTimeout = 86300000
            });
            _rabbitMQObjs.Add(new RabbitMQObj()
            {
                ExchangeName = "sendGenericEmail",
                FuncName = "sendGenericEmail",
                MessageTimeout = 86300000
            });
        }
        protected override async Task<ResultObj> DeclareConsumers()
        {
            var result = new ResultObj();
            try
            {
                await Parallel.ForEachAsync(_rabbitMQObjs, async (rabbitMQObj, cancellationToken) =>
                {
                    if (rabbitMQObj.ConnectChannel == null)
                    {
                        return;
                    }

                    rabbitMQObj.Consumer = new AsyncEventingBasicConsumer(rabbitMQObj.ConnectChannel);
                    await rabbitMQObj.ConnectChannel.BasicConsumeAsync(
                        queue: rabbitMQObj.QueueName,
                        autoAck: false,
                        consumer: rabbitMQObj.Consumer);

                    switch (rabbitMQObj.FuncName)
                    {
                        case "serviceWakeUp":
                            await RegisterConsumerHandlerAsync(rabbitMQObj, 1, "serviceWakeUp", async (_, _) => { result = await WakeUp(); });
                            break;
                        case "alertMessageInit":
                            await RegisterConsumerHandlerAsync(rabbitMQObj, 1, "alertMessageinit", (model, ea) =>
                            {
                                result = AlertMessageInit(ConvertToObject<AlertServiceInitObj>(model, ea));
                                return Task.CompletedTask;
                            });
                            break;
                        case "alertMessageResetAlerts":
                            await RegisterConsumerHandlerAsync(rabbitMQObj, 10, "alertMessageResetAlerts", (model, ea) =>
                            {
                                result = AlertMessageResetAlerts(ConvertToObject<AlertServiceAlertObj>(model, ea));
                                return Task.CompletedTask;
                            });
                            break;
                        case "alertMessageResetPredictAlerts":
                            await RegisterConsumerHandlerAsync(rabbitMQObj, 10, "alertMessageResetPredictAlerts", (model, ea) =>
                            {
                                result = AlertMessageResetPredictAlerts(ConvertToObject<AlertServiceAlertObj>(model, ea));
                                return Task.CompletedTask;
                            });
                            break;
                        case "alertMessage":
                            await RegisterConsumerHandlerAsync(rabbitMQObj, 1, "alertMessage", async (model, ea) =>
                            {
                                result = await AlertMessage(ConvertToObject<AlertMessage>(model, ea));
                            });
                            break;
                        case "updateUserInfoAlertMessage":
                            await RegisterConsumerHandlerAsync(rabbitMQObj, 1, "updateUserInfoAlertMessage", async (model, ea) =>
                            {
                                result = await UpdateUserInfoAlertMessage(ConvertToObject<UserInfo>(model, ea));
                            });
                            break;
                        case "monitorAlert":
                            await RegisterConsumerHandlerAsync(rabbitMQObj, 1, "monitorAlert", async (_, _) => { result = await MonitorAlert(); });
                            break;
                        case "predictAlert":
                            await RegisterConsumerHandlerAsync(rabbitMQObj, 1, "predictAlert", async (_, _) => { result = await PredictAlert(); });
                            break;
                        case "alertUpdateMonitorStatusAlerts":
                            await RegisterConsumerHandlerAsync(rabbitMQObj, 10, "alertUpdateMonitorStatusAlerts", async (model, ea) =>
                            {
                                result = await AlertUpdateMonitorStatusAlerts(ConvertToString(model, ea));
                            });
                            break;
                        case "alertUpdatePredictStatusAlerts":
                            await RegisterConsumerHandlerAsync(rabbitMQObj, 10, "alertUpdatePredictStatusAlerts", async (model, ea) =>
                            {
                                result = await AlertUpdatePredictStatusAlerts(ConvertToString(model, ea));
                            });
                            break;
                        case "userHostExpire":
                            await RegisterConsumerHandlerAsync(rabbitMQObj, 10, "userHostExpire", async (model, ea) =>
                            {
                                result = await UserHostExpire(ConvertToList<List<GenericEmailObj>>(model, ea));
                            });
                            break;
                        case "userProcessorExpire":
                            await RegisterConsumerHandlerAsync(rabbitMQObj, 10, "userProcessorExpire", async (model, ea) =>
                            {
                                result = await UserProccesorExpire(ConvertToList<List<GenericEmailObj>>(model, ea));
                            });
                            break;
                        case "userUpgrade":
                            await RegisterConsumerHandlerAsync(rabbitMQObj, 10, "userUpgrade", async (model, ea) =>
                            {
                                result = await UserUpgrade(ConvertToList<List<GenericEmailObj>>(model, ea));
                            });
                            break;
                        case "sendHostReport":
                            await RegisterConsumerHandlerAsync(rabbitMQObj, 1, "sendHostReport", async (model, ea) =>
                            {
                                result = await SendHostReport(ConvertToObject<HostReportObj>(model, ea));
                            });
                            break;
                        case "sendGenericEmail":
                            await RegisterConsumerHandlerAsync(rabbitMQObj, 1, "sendGenericEmail", async (model, ea) =>
                            {
                                result = await SendGenericEmail(ConvertToObject<GenericEmailObj>(model, ea));
                            });
                            break;
                    }
                });
                result.Success = true;
                result.Message += " Success : Declared all consumers ";
            }
            catch (Exception e)
            {
                string message = " Error : failed to declate consumers. Error was : " + e.ToString() + " . ";
                result.Message += message;
                Console.WriteLine(result.Message);
                result.Success = false;
            }
            return result;
        }
        public async Task<ResultObj> WakeUp()
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : WakeUp : ";
            try
            {
                /*_alertMessageService.Awake=true;
                result.Message+="Success : Set Awake to true in AlertMessageService.";
                result.Success=true;*/
                result = await _alertMessageService.WakeUp();
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
        public ResultObj AlertMessageInit(AlertServiceInitObj? initObj)
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : AlertMessageInit : ";
            if (initObj == null)
            {
                result.Message += " Error : initObj is Null ";
                return result;
            }

            try
            {
                _alertMessageService.InitService(initObj);
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
        public ResultObj AlertMessageResetAlerts(AlertServiceAlertObj? alertServiceAlertObj)
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : AlertMessageResetAlerts : ";
            if (alertServiceAlertObj == null)
            {
                result.Message += " Error : alertServiceAlertObj is Null ";
                return result;
            }
            if (_alertMessageService.IsBadAuthKey(alertServiceAlertObj.AuthKey, alertServiceAlertObj.AppID))
            {
                result.Message += " Error : alertServiceAlertObj is invalid ";
                return result;
            }
            if (!ValidatePublisherIdentityForApp(
                result,
                alertServiceAlertObj.AppID,
                "AlertMessageResetAlerts",
                allowDefaultPublisher: true))
            {
                return result;
            }
            try
            {

                var results = _alertMessageService.ResetMonitorAlerts(alertServiceAlertObj.AlertFlagObjs);
                results.ForEach(f => result.Message += f.Message);
                result.Success = results.All(a => a.Success == true) && results.Count() != 0;
                result.Data = results;
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

        public ResultObj AlertMessageResetPredictAlerts(AlertServiceAlertObj? alertServiceAlertObj)
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : AlertMessageResetPredictAlerts : ";
            if (alertServiceAlertObj == null)
            {
                result.Message += " Error : alertServiceAlertObj is Null ";
                return result;
            }
            if (_alertMessageService.IsBadAuthKey(alertServiceAlertObj.AuthKey, alertServiceAlertObj.AppID))
            {
                result.Message += " Error : alertServiceAlertObj is invalid ";
                return result;
            }
            if (!ValidatePublisherIdentityForApp(
                result,
                alertServiceAlertObj.AppID,
                "AlertMessageResetPredictAlerts",
                allowDefaultPublisher: true))
            {
                return result;
            }
            try
            {

                var results = _alertMessageService.ResetPredictAlerts(alertServiceAlertObj.AlertFlagObjs);
                results.ForEach(f => result.Message += f.Message);
                result.Success = results.All(a => a.Success == true) && results.Count() != 0;
                result.Data = results;
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
        public async Task<ResultObj> AlertMessage(AlertMessage? alertMessage)
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : AlertMessage : ";
            if (alertMessage == null)
            {
                result.Message += " Error : alertMessage is Null ";
                return result;
            }
            try
            {
                result = await _alertMessageService.Send(alertMessage);
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
        public async Task<ResultObj> UpdateUserInfoAlertMessage(UserInfo? userInfo)
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : UpdateUserInfoAlertMessage : ";
            if (userInfo == null)
            {
                result.Message += " Error : userInfo is Null ";
                return result;
            }
            try
            {
                result = await _alertMessageService.UpdateUserInfo(userInfo);
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
        public async Task<ResultObj> MonitorAlert()
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : MonitorAlert : ";
            try
            {
                result = await _alertMessageService.MonitorAlert();
                //_logger.LogInformation(result.Message);
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
        public async Task<ResultObj> PredictAlert()
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : PredictAlert : ";
            try
            {
                result = await _alertMessageService.PredictAlert();
                //_logger.LogInformation(result.Message);
            }
            catch (Exception e)
            {
                result.Data = null;
                result.Success = false;
                result.Message += "Error : Failed to run PredictAlert : Error was : " + e.Message + " ";
                _logger.LogError("Error : Failed to run PredictAlert : Error was : " + e.Message + " ");
            }
            return result;
        }
        public async Task<ResultObj> AlertUpdateMonitorStatusAlerts(string? monitorStatusAlertString)
        {
            var result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : alertUpdateMonitorStatusAlerts : ";
            if (monitorStatusAlertString == null)
            {
                result.Message += " Error : monitorStatusAlertString is Null ";
                return result;
            }
            try
            {
                while (_alertMessageService.IsMonitorAlertRunning)
                {
                    result.Message += "Info : Waiting for Alert to stop running ";
                    new System.Threading.ManualResetEvent(false).WaitOne(5000);
                }
                _alertMessageService.IsMonitorAlertRunning = true;
                var returnResult = await _dataQueueService.AddProcessorDataStringToQueue(monitorStatusAlertString, _alertMessageService.MonitorAlerts);
                _alertMessageService.IsMonitorAlertRunning = false;
                result.Message += returnResult.Message;
                result.Success = returnResult.Success;
                result.Data = null;
                _logger.LogDebug("AlertMonitorStatusAlerts : " + JsonUtils.WriteJsonObjectToString(_alertMessageService.MonitorAlerts.ToList()));
            }
            catch (Exception e)
            {
                result.Success = false;
                result.Message += "Error : Failed to set AlertMonitorStatusAlerts : Error was : " + e.Message + " ";
                _logger.LogError("Error : Failed to set AlertMonitorStatusAlerts : Error was : " + e.Message + " ");
            }
            return result;
        }


        public async Task<ResultObj> AlertUpdatePredictStatusAlerts(string? predictStatusAlertString)
        {
            var result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : alertUpdatePredictStatusAlerts : ";
            if (predictStatusAlertString == null)
            {
                result.Message += " Error : predictStatusAlertString is Null ";
                return result;
            }
            try
            {
                while (_alertMessageService.IsPredictAlertRunning)
                {
                    result.Message += "Info : Waiting for Alert to stop running ";
                    new System.Threading.ManualResetEvent(false).WaitOne(5000);
                }
                _alertMessageService.IsPredictAlertRunning = true;
                var returnResult = await _dataQueueService.AddPredictDataStringToQueue(predictStatusAlertString, _alertMessageService.PredictAlerts);
                _alertMessageService.IsPredictAlertRunning = false;
                result.Message += returnResult.Message;
                result.Success = returnResult.Success;
                result.Data = null;
                _logger.LogDebug("AlertPredictStatusAlerts : " + JsonUtils.WriteJsonObjectToString(_alertMessageService.PredictAlerts.ToList()));
            }
            catch (Exception e)
            {
                result.Success = false;
                result.Message += "Error : Failed to set AlertPredictStatusAlerts : Error was : " + e.Message + " ";
                _logger.LogError("Error : Failed to set AlertPredictStatusAlerts : Error was : " + e.Message + " ");
            }
            return result;
        }
        public async Task<ResultObj> UserHostExpire(List<GenericEmailObj>? emailObjs)
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : UserHostExpire : ";
            if (emailObjs == null)
            {
                result.Message += " Error : emailObjs is Null ";
                return result;
            }
            try
            {
                var results = await _alertMessageService.UserHostExpire(emailObjs);
                results.ForEach(f => result.Message += f.Message);
                result.Success = results.All(a => a.Success == true) && results.Count() != 0;
                result.Data = results;
                if (result.Success)
                {
                    _logger.LogInformation(result.Message);
                }
                else
                {
                    _logger.LogError(result.Message);
                }

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

        public async Task<ResultObj> UserProccesorExpire(List<GenericEmailObj>? emailObjs)
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : UserProcessorExpire : ";
            if (emailObjs == null)
            {
                result.Message += " Error : emailObjs is Null ";
                return result;
            }
            try
            {
                var results = await _alertMessageService.UserProcessorExpire(emailObjs);
                results.ForEach(f => result.Message += f.Message);
                result.Success = results.All(a => a.Success == true) && results.Count() != 0;
                result.Data = results;
                if (result.Success)
                {
                    _logger.LogInformation(result.Message);
                }
                else
                {
                    _logger.LogError(result.Message);
                }

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


        public async Task<ResultObj> UserUpgrade(List<GenericEmailObj>? emailObjs)
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : UserUpgrade : ";
            if (emailObjs == null)
            {
                result.Message += " Error : emailObjs is Null ";
                return result;
            }
            try
            {
                var results = await _alertMessageService.UpgradeAccounts(emailObjs);
                results.ForEach(f => result.Message += f.Message);
                result.Success = results.All(a => a.Success == true) && results.Count() != 0;
                result.Data = results;
                if (result.Success)
                {
                    _logger.LogInformation(result.Message);
                }
                else
                {
                    _logger.LogError(result.Message);
                }

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

        public async Task<ResultObj> SendHostReport(HostReportObj? hostReport)
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : SendHostReport : ";
            if (hostReport == null)
            {
                result.Success = false;
                result.Message += " Error : hostReport is null . ";
                return result;
            }
            try
            {
                result = await _alertMessageService.SendHostReport(hostReport);
                _logger.LogInformation(result.Message);
            }
            catch (Exception e)
            {
                result.Data = null;
                result.Success = false;
                result.Message += "Error : Failed to run SendHostReport : Error was : " + e.Message + " ";
                _logger.LogError("Error : Failed to run SendHostReport : Error was : " + e.Message + " ");
            }
            return result;
        }

        public async Task<ResultObj> SendGenericEmail(GenericEmailObj? genericEmail)
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : SendGenericEmail : ";
            if (genericEmail == null)
            {
                result.Success = false;
                result.Message += " Error : genericEmail is null . ";
                return result;
            }
            try
            {
                result = await _alertMessageService.SendGenericEmail(genericEmail);
                _logger.LogInformation(result.Message);
            }
            catch (Exception e)
            {
                result.Data = null;
                result.Success = false;
                result.Message += "Error : Failed to run SendGenericEmail : Error was : " + e.Message + " ";
                _logger.LogError("Error : Failed to run SendGenericEmail : Error was : " + e.Message + " ");
            }
            return result;
        }

    }
}
