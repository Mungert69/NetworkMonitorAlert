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
namespace NetworkMonitor.Service.Services
{
    public interface IDataQueueService
    {
        Task<ResultObj> AddProcessorDataBytesToQueue(byte[] processorDataBytes,List<MonitorStatusAlert> monitorStatusAlerts);
   
    }
    public class DataQueueService : IDataQueueService
    {
        private ILogger<DataQueueService> _logger;
        private TaskQueue taskQueue = new TaskQueue();
        public DataQueueService(ILogger<DataQueueService> logger)
        {
   
            _logger = logger;
        }
        public Task<ResultObj> AddProcessorDataBytesToQueue(byte[] processorDataBytes,List<MonitorStatusAlert> monitorStatusAlerts)
        {
            Func<byte[],List<MonitorStatusAlert>, Task<ResultObj>> func = CommitProcessorDataBytes;
            return taskQueue.EnqueueStatusBytes<ResultObj>(func, processorDataBytes,monitorStatusAlerts);
        }
      
        private Task<ResultObj> CommitProcessorDataBytes(byte[] processorDataBytes,List<MonitorStatusAlert> monitorStatusAlerts)
        {
            return Task<ResultObj>.Run(() =>
            {
                _logger.LogInformation("Started CommitProcessorDataBytes at "+DateTime.UtcNow);
                var result = new ResultObj();
                try
                {              
                     var processorDataObj =ProcessorDataBuilder.MergeMonitorStatusAlerts(processorDataBytes,monitorStatusAlerts);           
                    result.Success=true;
                    _logger.LogInformation("Finshed CommitProcessorDataBytes at "+DateTime.UtcNow+ " for Processor AppID "+processorDataObj.AppID);
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