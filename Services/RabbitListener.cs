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
    }

    public class RabbitListener : RabbitListenerBase, IRabbitListener
    {
        private IAlertMessageService _alertMessageService;
        private IDataQueueService _dataQueueService;
        public RabbitListener(IAlertMessageService alertMessageService, IDataQueueService dataQueueService, ILogger<RabbitListenerBase> logger, ISystemParamsHelper systemParamsHelper) : base(logger, DeriveSystemUrl(systemParamsHelper))
        {
            _alertMessageService = alertMessageService;
            _dataQueueService = dataQueueService;
            Setup();
        }



        private static SystemUrl DeriveSystemUrl(ISystemParamsHelper systemParamsHelper)
        {
            return systemParamsHelper.GetSystemParams().ThisSystemUrl;
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
        protected override ResultObj DeclareConsumers()
        {
            var result = new ResultObj();
            try
            {
                _rabbitMQObjs.ForEach(rabbitMQObj =>
            {
                rabbitMQObj.Consumer = new EventingBasicConsumer(rabbitMQObj.ConnectChannel);
                if (rabbitMQObj.ConnectChannel != null)
                {
                    switch (rabbitMQObj.FuncName)
                    {
                        case "serviceWakeUp":
                            rabbitMQObj.ConnectChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
                            rabbitMQObj.Consumer.Received += async (model, ea) =>
                        {
                            try
                            {
                                result = await WakeUp();
                                rabbitMQObj.ConnectChannel.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(" Error : RabbitListener.DeclareConsumers.serviceWakeUp " + ex.Message);
                            }
                        };
                            break;
                        case "alertMessageInit":
                            rabbitMQObj.ConnectChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
                            rabbitMQObj.Consumer.Received += (model, ea) =>
                        {
                            try
                            {
                                result = AlertMessageInit(ConvertToObject<AlertServiceInitObj>(model, ea));
                                rabbitMQObj.ConnectChannel.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(" Error : RabbitListener.DeclareConsumers.alertMessageinit " + ex.Message);
                            }
                            result = AlertMessageInit(ConvertToObject<AlertServiceInitObj>(model, ea));
                            rabbitMQObj.ConnectChannel.BasicAck(ea.DeliveryTag, false);
                        };
                            break;
                        case "alertMessageResetAlerts":
                            rabbitMQObj.ConnectChannel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);
                            rabbitMQObj.Consumer.Received += (model, ea) =>
                        {
                            try
                            {
                                result = AlertMessageResetAlerts(ConvertToObject<AlertServiceAlertObj>(model, ea));
                                rabbitMQObj.ConnectChannel.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(" Error : RabbitListener.DeclareConsumers.alertMessageResetAlerts " + ex.Message);
                            }
                        };
                            break;
                             case "alertMessageResetPredictAlerts":
                            rabbitMQObj.ConnectChannel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);
                            rabbitMQObj.Consumer.Received += (model, ea) =>
                        {
                            try
                            {
                                result = AlertMessageResetPredictAlerts(ConvertToObject<AlertServiceAlertObj>(model, ea));
                                rabbitMQObj.ConnectChannel.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(" Error : RabbitListener.DeclareConsumers.alertMessageResetPredictAlerts " + ex.Message);
                            }
                        };
                            break;
                        case "alertMessage":
                            rabbitMQObj.ConnectChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
                            rabbitMQObj.Consumer.Received += async (model, ea) =>
                        {
                            try
                            {
                                result = await AlertMessage(ConvertToObject<AlertMessage>(model, ea));
                                rabbitMQObj.ConnectChannel.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(" Error : RabbitListener.DeclareConsumers.alertMessage " + ex.Message);
                            }
                        };
                            break;
                        case "updateUserInfoAlertMessage":
                            rabbitMQObj.ConnectChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
                            rabbitMQObj.Consumer.Received += async (model, ea) =>
                        {
                            try
                            {
                                result = await UpdateUserInfoAlertMessage(ConvertToObject<UserInfo>(model, ea));
                                rabbitMQObj.ConnectChannel.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(" Error : RabbitListener.DeclareConsumers.updateUserInfoAlertMessage " + ex.Message);
                            }
                        };
                            break;
                        case "monitorAlert":
                            rabbitMQObj.ConnectChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
                            rabbitMQObj.Consumer.Received += async (model, ea) =>
                        {
                            try
                            {
                                result = await MonitorAlert();
                                rabbitMQObj.ConnectChannel.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(" Error : RabbitListener.DeclareConsumers.monitorAlert " + ex.Message);
                            }
                        };
                            break;
                         case "predictAlert":
                            rabbitMQObj.ConnectChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
                            rabbitMQObj.Consumer.Received += async (model, ea) =>
                        {
                            try
                            {
                                result = await PredictAlert();
                                rabbitMQObj.ConnectChannel.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(" Error : RabbitListener.DeclareConsumers.predictAlert " + ex.Message);
                            }
                        };
                            break;
                        case "alertUpdateMonitorStatusAlerts":
                            rabbitMQObj.ConnectChannel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);
                            rabbitMQObj.Consumer.Received += async (model, ea) =>
                        {
                            try
                            {
                                result = await AlertUpdateMonitorStatusAlerts(ConvertToString(model, ea));
                                rabbitMQObj.ConnectChannel.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(" Error : RabbitListener.DeclareConsumers.alertUpdateMonitorStatusAlerts " + ex.Message);
                            }
                        };
                            break;
                         case "alertUpdatePredictStatusAlerts":
                            rabbitMQObj.ConnectChannel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);
                            rabbitMQObj.Consumer.Received += async (model, ea) =>
                        {
                            try
                            {
                                result = await AlertUpdatePredictStatusAlerts(ConvertToString(model, ea));
                                rabbitMQObj.ConnectChannel.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(" Error : RabbitListener.DeclareConsumers.alertUpdatePredictStatusAlerts " + ex.Message);
                            }
                        };
                            break;
                        case "userHostExpire":
                            rabbitMQObj.ConnectChannel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);
                            rabbitMQObj.Consumer.Received += async (model, ea) =>
                        {
                            try
                            {
                                result = await UserHostExpire(ConvertToList<List<GenericEmailObj>>(model, ea));
                                rabbitMQObj.ConnectChannel.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(" Error : RabbitListener.DeclareConsumers.userHostExpire " + ex.Message);
                            }
                        };
                            break;
                         case "userUpgrade":
                            rabbitMQObj.ConnectChannel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);
                            rabbitMQObj.Consumer.Received += async (model, ea) =>
                        {
                            try
                            {
                                result = await UserUpgrade(ConvertToList<List<GenericEmailObj>>(model, ea));
                                rabbitMQObj.ConnectChannel.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(" Error : RabbitListener.DeclareConsumers.userUpgrade " + ex.Message);
                            }
                        };
                            break;
                        case "sendHostReport":
                            rabbitMQObj.ConnectChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
                            rabbitMQObj.Consumer.Received += async (model, ea) =>
                        {
                            try
                            {
                                result = await SendHostReport(ConvertToObject<HostReportObj>(model, ea));
                                rabbitMQObj.ConnectChannel.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(" Error : RabbitListener.DeclareConsumers.alertMessage " + ex.Message);
                            }
                        };
                            break;
                        case "sendGenericEmail":
                            rabbitMQObj.ConnectChannel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
                            rabbitMQObj.Consumer.Received += async (model, ea) =>
                        {
                            try
                            {
                                result = await SendGenericEmail(ConvertToObject<GenericEmailObj>(model, ea));
                                rabbitMQObj.ConnectChannel.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(" Error : RabbitListener.DeclareConsumers.sendGenericEmail " + ex.Message);
                            }
                        };
                            break;
                    }
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
          public async Task<ResultObj> PredictAlert()
        {
            ResultObj result = new ResultObj();
            result.Success = false;
            result.Message = "MessageAPI : PredictAlert : ";
            try
            {
                result = await _alertMessageService.PredictAlert();
                _logger.LogInformation(result.Message);
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
