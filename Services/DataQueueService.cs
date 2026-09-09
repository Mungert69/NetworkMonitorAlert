using System;
using System.Collections.Generic;
using System.Linq;
using NetworkMonitor.Objects;
using NetworkMonitor.Objects.ServiceMessage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using NetworkMonitorService.Objects.ServiceMessage;
using System.Threading.Tasks;
using NetworkMonitor.Objects.Factory;
using NetworkMonitor.Utils.Helpers;
using NetworkMonitor.Utils;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NetworkMonitor.Objects.Repository;
using System.Security.Cryptography;
using System.Text;

namespace NetworkMonitor.Alert.Services
{
    public interface IDataQueueService
    {
        Task<ResultObj> AddProcessorDataStringToQueue(
            string processorDataString,
            List<IAlertable> monitorStatusAlerts,
            string publisherUserId = "",
            bool requirePublisherUserId = false);
        Task<ResultObj> AddPredictDataStringToQueue(string processorDataString, List<IAlertable> predictStatusAlerts);

    }
    public class DataQueueService : IDataQueueService
    {
        private ILogger _logger;
        private TaskQueue taskQueue = new TaskQueue();
        private string _encryptKey;
        private readonly IProcessorState _processorState;
        private readonly IBackendMessageHmacService _backendHmac;
        public DataQueueService(ILogger<DataQueueService> logger, ISystemParamsHelper systemParamsHelper, IProcessorState processorState, IBackendMessageHmacService backendHmac)
        {
            _encryptKey = systemParamsHelper.GetSystemParams().EmailEncryptKey;
            _logger = logger;
            _processorState = processorState;
            _backendHmac = backendHmac;
        }
        public Task<ResultObj> AddProcessorDataStringToQueue(
            string processorDataString,
            List<IAlertable> monitorStatusAlerts,
            string publisherUserId = "",
            bool requirePublisherUserId = false)
        {
            Func<string, List<IAlertable>, Task<ResultObj>> func = (data, alerts) =>
                CommitProcessorDataString(data, alerts, publisherUserId, requirePublisherUserId);
            return taskQueue.EnqueueStatusString<ResultObj>(func, processorDataString, monitorStatusAlerts);
        }

        private Task<ResultObj> CommitProcessorDataString(
            string processorDataString,
            List<IAlertable> monitorStatusAlerts,
            string publisherUserId,
            bool requirePublisherUserId)
        {
            return Task<ResultObj>.Run(() =>
            {
                _logger.LogInformation("Started CommitProcessorDataBytes at " + DateTime.UtcNow);
                var result = new ResultObj();
                try
                {
                    ProcessorDataObj? processorDataObj = ProcessorDataBuilder.ExtractFromZString<ProcessorDataObj>(processorDataString);

                    if (processorDataObj == null)
                    {
                        result.Success = false;
                        result.Message = " Error : Failed CommitProcessorDataBytes processorDataObj is null.";
                        _logger.LogError(result.Message);
                        return result;
                    }
                    if (processorDataObj.AppID == null)
                    {
                        result.Success = false;
                        result.Message = " Error : Failed CommitProcessorDataBytes processorDataObj.AppID is null.";
                        _logger.LogError(result.Message);
                        return result;
                    }
                    if (requirePublisherUserId && !IsPublisherAuthorizedForApp(publisherUserId, processorDataObj.AppID))
                    {
                        result.Success = false;
                        result.Message = $" Error : Failed CommitProcessorDataBytes AppID '{processorDataObj.AppID}' is not bound to publisher '{publisherUserId}'.";
                        _logger.LogWarning(result.Message);
                        return result;
                    }
                    if (processorDataObj.AuthKey == null)
                    {
                        result.Success = false;
                        result.Message = $" Error : Failed CommitProcessorDataBytes processorDataObj.AppKey is null for AppID {processorDataObj.AppID}";
                        _logger.LogError(result.Message);
                        return result;
                    }
                    if (EncryptHelper.IsBadKey(_encryptKey, processorDataObj.AuthKey, processorDataObj.AppID))
                    {
                        result.Success = false;
                        result.Message = $" Error : Failed CommitProcessorDataBytes bad AuthKey for AppID {processorDataObj.AppID}";
                        _logger.LogError(result.Message);
                        return result;
                    }
                    if (!IsCurrentAuthKey(processorDataObj.AppID, processorDataObj.AuthKey))
                    {
                        result.Success = false;
                        result.Message = $" Error : Failed CommitProcessorDataBytes expired AuthKey for AppID {processorDataObj.AppID}";
                        _logger.LogWarning(result.Message);
                        return result;
                    }
                    if (processorDataObj.MonitorStatusAlerts.Where(w => w.AppID != processorDataObj.AppID).Count() > 0)
                    {
                        result.Success = false;
                        result.Message = $" Error : Failed CommitProcessorDataBytes invalid AppID in data for AppID {processorDataObj.AppID}";
                        _logger.LogError(result.Message);
                        return result;
                    }

                    processorDataObj = ProcessorDataBuilder.MergeMonitorStatusAlerts(processorDataObj, monitorStatusAlerts);

                    if (processorDataObj == null)
                    {
                        result.Success = false;
                        result.Message = " Error : Failed CommitProcessorDataBytes no ProcessorDataObj data";
                        _logger.LogError(result.Message);

                    }
                    else
                    {
                        result.Success = true;
                        result.Message = " Success : Finshed CommitProcessorDataBytes at " + DateTime.UtcNow + " for Processor AppID " + processorDataObj.AppID + ". ";
                        _logger.LogInformation(result.Message);
                    }
                }
                catch (Exception e)
                {
                    result.Success = false;
                    result.Message += "Error : failed to process Data. Error was : " + e.Message.ToString();
                    _logger.LogError(result.Message);
                }
                return result;
            });
        }

        public async Task<ResultObj> AddPredictDataStringToQueue(string predictDataString, List<IAlertable> predictStatusAlerts)
        {
            ProcessorDataObj? message;
            try { message = ProcessorDataBuilder.ExtractFromZString<ProcessorDataObj>(predictDataString); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Rejected alertUpdatePredictStatusAlerts: invalid compressed payload.");
                return new ResultObj { Success = false, Message = " Error : invalid predict status payload." };
            }
            if (!MessageSecurityPolicyRegistry.Requires("alertUpdatePredictStatusAlerts", "alertUpdatePredictStatusAlerts", MessageProtection.BackendHmac) ||
                message == null || !await _backendHmac.VerifyAsync("alertUpdatePredictStatusAlerts", "alertUpdatePredictStatusAlerts", message))
            {
                _logger.LogWarning("Rejected alertUpdatePredictStatusAlerts: invalid backend HMAC.");
                return new ResultObj { Success = false, Message = " Error : invalid backend HMAC." };
            }
            Func<string, List<IAlertable>, Task<ResultObj>> func = CommitPredictDataString;
            return await taskQueue.EnqueueStatusString<ResultObj>(func, predictDataString, predictStatusAlerts);
        }

        private Task<ResultObj> CommitPredictDataString(string predictDataString, List<IAlertable> predictStatusAlerts)
        {
            return Task<ResultObj>.Run(() =>
            {
                _logger.LogInformation("Started CommitProcessorDataBytes at " + DateTime.UtcNow);
                var result = new ResultObj();
                try
                {
                    ProcessorDataObj? processorDataObj = ProcessorDataBuilder.ExtractFromZString<ProcessorDataObj>(predictDataString);

                    if (processorDataObj == null)
                    {
                        result.Success = false;
                        result.Message = " Error : Failed CommitProcessorDataBytes processorDataObj is null.";
                        _logger.LogError(result.Message);
                        return result;
                    }
                    if (processorDataObj.AppID == null)
                    {
                        result.Success = false;
                        result.Message = " Error : Failed CommitProcessorDataBytes processorDataObj.AppID is null.";
                        _logger.LogError(result.Message);
                        return result;
                    }
                    if (processorDataObj.PredictStatusAlerts.Where(w => w.AppID != processorDataObj.AppID).Count() > 0)
                    {
                        result.Success = false;
                        result.Message = $" Error : Failed CommitPredictDataBytes invalid AppID in data for AppID {processorDataObj.AppID}";
                        _logger.LogError(result.Message);
                        return result;
                    }

                    processorDataObj = ProcessorDataBuilder.MergePredictStatusAlerts(processorDataObj, predictStatusAlerts);

                    if (processorDataObj == null)
                    {
                        result.Success = false;
                        result.Message = " Error : Failed CommitProcessorDataBytes no ProcessorDataObj data";
                        _logger.LogError(result.Message);

                    }
                    else
                    {
                        result.Success = true;
                        result.Message = " Success : Finshed CommitProcessorDataBytes at " + DateTime.UtcNow + " for Processor AppID " + processorDataObj.AppID + ". ";
                        _logger.LogInformation(result.Message);
                    }
                }
                catch (Exception e)
                {
                    result.Success = false;
                    result.Message += "Error : failed to process Data. Error was : " + e.Message.ToString();
                    _logger.LogError(result.Message);
                }
                return result;
            });
        }

        private bool IsCurrentAuthKey(string appID, string suppliedAuthKey)
        {
            var currentAuthKey = _processorState.GetProcessorFromID(appID, true)?.AuthKey;
            if (string.IsNullOrEmpty(currentAuthKey))
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(suppliedAuthKey),
                Encoding.UTF8.GetBytes(currentAuthKey));
        }

        internal static bool IsPublisherAuthorizedForApp(string? publisherUserId, string? appId)
        {
            if (string.IsNullOrWhiteSpace(publisherUserId) ||
                string.IsNullOrWhiteSpace(appId))
            {
                return false;
            }

            // System processors are allowed to send any AppID.
            if (string.Equals(publisherUserId, "systemprocessor", StringComparison.Ordinal))
            {
                return true;
            }

            return appId.StartsWith(publisherUserId + "-", StringComparison.Ordinal);
        }

    }
}
