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
namespace NetworkMonitor.Alert.Services
{
    public interface IDataQueueService
    {
        Task<ResultObj> AddProcessorDataStringToQueue(string processorDataString, List<MonitorStatusAlert> monitorStatusAlerts);

    }
    public class DataQueueService : IDataQueueService
    {
        private ILogger _logger;
        private TaskQueue taskQueue = new TaskQueue();
        public DataQueueService(ILogger<DataQueueService> logger)
        {

            _logger = logger;
        }
        public Task<ResultObj> AddProcessorDataStringToQueue(string processorDataString, List<MonitorStatusAlert> monitorStatusAlerts)
        {
            Func<string, List<MonitorStatusAlert>, Task<ResultObj>> func = CommitProcessorDataString;
            return taskQueue.EnqueueStatusString<ResultObj>(func, processorDataString, monitorStatusAlerts);
        }

        private Task<ResultObj> CommitProcessorDataString(string processorDataString, List<MonitorStatusAlert> monitorStatusAlerts)
        {
            return Task<ResultObj>.Run(() =>
            {
                _logger.LogInformation("Started CommitProcessorDataBytes at " + DateTime.UtcNow);
                var result = new ResultObj();
                try
                {
                    var processorDataObj = ProcessorDataBuilder.MergeMonitorStatusAlerts(processorDataString, monitorStatusAlerts);
                    if (processorDataObj == null || processorDataObj.AppID == null)
                    {
                        result.Success = false;
                        result.Message=" Error : Failed CommitProcessorDataBytes no ProcessorDataObj or AppID found";
                        _logger.LogError(result.Message);

                    }
                    else
                    {
                        result.Success = true;
                        result.Message=" Success : Finshed CommitProcessorDataBytes at " + DateTime.UtcNow + " for Processor AppID " + processorDataObj.AppID+ ". ";
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