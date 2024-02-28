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

namespace NetworkMonitor.Alert.Services
{
    public interface IDataQueueService
    {
        Task<ResultObj> AddProcessorDataStringToQueue(string processorDataString, List<IAlertable> monitorStatusAlerts);
        Task<ResultObj> AddPredictDataStringToQueue(string processorDataString, List<IAlertable> predictStatusAlerts);

    }
    public class DataQueueService : IDataQueueService
    {
        private ILogger _logger;
        private TaskQueue taskQueue = new TaskQueue();
        private string _encryptKey;
        public DataQueueService(ILogger<DataQueueService> logger, ISystemParamsHelper systemParamsHelper)
        {
            _encryptKey = systemParamsHelper.GetSystemParams().EmailEncryptKey;
            _logger = logger;
        }
        public Task<ResultObj> AddProcessorDataStringToQueue(string processorDataString, List<IAlertable> monitorStatusAlerts)
        {
            Func<string, List<IAlertable>, Task<ResultObj>> func = CommitProcessorDataString;
            return taskQueue.EnqueueStatusString<ResultObj>(func, processorDataString, monitorStatusAlerts);
        }

        private Task<ResultObj> CommitProcessorDataString(string processorDataString, List<IAlertable> monitorStatusAlerts)
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
                    if (processorDataObj.AuthKey == null)
                    {
                        result.Success = false;
                        result.Message = $" Error : Failed CommitProcessorDataBytes processorDataObj.AppKey is null for AppID {processorDataObj.AppID}";
                        _logger.LogError(result.Message);
                        return result;
                    }
                    if (EncryptionHelper.IsBadKey(_encryptKey, processorDataObj.AuthKey, processorDataObj.AppID))
                    {
                        result.Success = false;
                        result.Message = $" Error : Failed CommitProcessorDataBytes bad AuthKey for AppID {processorDataObj.AppID}";
                        _logger.LogError(result.Message);
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

        public Task<ResultObj> AddPredictDataStringToQueue(string predictDataString, List<IAlertable> predictStatusAlerts)
        {
            Func<string, List<IAlertable>, Task<ResultObj>> func = CommitPredictDataString;
            return taskQueue.EnqueueStatusString<ResultObj>(func, predictDataString, predictStatusAlerts);
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
                    if (processorDataObj.AuthKey == null)
                    {
                        result.Success = false;
                        result.Message = $" Error : Failed CommitProcessorDataBytes processorDataObj.AppKey is null for AppID {processorDataObj.AppID}";
                        _logger.LogError(result.Message);
                        return result;
                    }
                    if (EncryptionHelper.IsBadKey(_encryptKey, processorDataObj.AuthKey, processorDataObj.AppID))
                    {
                        result.Success = false;
                        result.Message = $" Error : Failed CommitProcessorDataBytes bad AuthKey for AppID {processorDataObj.AppID}";
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

    }
}