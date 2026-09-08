using System;
using System.Collections.Generic;
using System.Linq;
using NetworkMonitor.Objects.ServiceMessage;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetworkMonitor.Objects.Repository.Helpers;

namespace NetworkMonitor.Objects.Repository
{
    public class PublishAlertsRepo
    {
        public static async Task ProcessorAlertSent(ILogger logger, IRabbitRepo rabbitRepo, List<IAlertable> publishAlertSentList, List<ProcessorObj> processorList)
        {
            try
            {
                foreach (var processorObj in processorList)
                {
                    List<int> monitorStatusAlertIDs = publishAlertSentList.Where(w => w.AppID == processorObj.AppID).Select(s => s.ID).ToList();
                    if (monitorStatusAlertIDs.Count != 0)
                    {
                        await ProcessorRabbitPublisher.PublishAsync(rabbitRepo, processorObj.AppID, "processorAlertSent", monitorStatusAlertIDs, processorObj.RabbitTopologyVersion);
                        logger.LogInformation("Sent event processorAlertSent for AppID  " + processorObj.AppID);
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogCritical("Error : Unable to send processorAlertSent message. Error was :  " + e.Message.ToString());
            }
        }
        public static async Task ProcessorAlertFlag(ILogger logger, IRabbitRepo rabbitRepo, List<IAlertable> publishAlertFlagList, List<ProcessorObj> processorList)
        {
            foreach (var processorObj in processorList)
            {
                List<int> monitorStatusAlertIDs = publishAlertFlagList.Where(w => w.AppID == processorObj.AppID).Select(s => s.ID).ToList();
                if (monitorStatusAlertIDs.Count != 0)
                {
                    await ProcessorRabbitPublisher.PublishAsync(rabbitRepo, processorObj.AppID, "processorAlertFlag", monitorStatusAlertIDs, processorObj.RabbitTopologyVersion);
                    logger.LogInformation("Sent event processorAlertFlag for AppID  " + processorObj.AppID);
                }
                publishAlertFlagList.Where(w => w.AppID == processorObj.AppID).ToList().ForEach(f => f.AlertFlag = true);
            }
        }
        public static async Task ProcessorResetAlerts(ILogger logger, IRabbitRepo rabbitRepo, Dictionary<string, List<int>> monitorIPDic, List<ProcessorObj>? processors = null)
        {
            try
            {
                foreach (KeyValuePair<string, List<int>> kvp in monitorIPDic)
                {
                    var monitorIPIDs = new List<int>(kvp.Value);
                    // Dont publish this at the moment as its causing alerts to refire.
                    int version = processors?.FirstOrDefault(p => p.AppID == kvp.Key)?.RabbitTopologyVersion
                        ?? ProcessorRabbitTopology.LegacyVersion;
                    await ProcessorRabbitPublisher.PublishAsync(rabbitRepo, kvp.Key, "processorResetAlerts", monitorIPIDs, version);
                }
            }
            catch (Exception e)
            {
                logger.LogError(" Error : failed to publish ProcessResetAlerts. Error was :" + e.ToString());
            }
        }
        public static async Task PredictAlertSent(ILogger logger, IRabbitRepo rabbitRepo, List<IAlertable> publishAlertSentList)
        {
            try
            {
                List<int> predictStatusAlertIDs = publishAlertSentList.Select(s => s.ID).ToList();
                if (predictStatusAlertIDs.Count != 0)
                {
                    await rabbitRepo.PublishAsync("predictAlertSent", new BackendIntListMessage { Values = predictStatusAlertIDs });
                    logger.LogInformation("Sent event predictAlertSent ");
                }

            }
            catch (Exception e)
            {
                logger.LogCritical("Error : Unable to send predictAlertSent message. Error was :  " + e.Message.ToString());
            }
        }
        public static async Task PredictAlertFlag(ILogger logger, IRabbitRepo rabbitRepo, List<IAlertable> publishAlertFlagList)
        {

            List<int> predictStatusAlertIDs = publishAlertFlagList.Select(s => s.ID).ToList();
            if (predictStatusAlertIDs.Count != 0)
            {
                await rabbitRepo.PublishAsync("predictAlertFlag", new BackendIntListMessage { Values = predictStatusAlertIDs });
                logger.LogInformation("Sent event predictAlertFlag ");
            }
            publishAlertFlagList.ToList().ForEach(f => f.AlertFlag = true);

        }
        public static async Task PredictResetAlerts(ILogger logger, IRabbitRepo rabbitRepo, List<int> monitorIPIDs)
        {
            try
            {

                // Dont publish this at the moment as its causing alerts to refire?
                await rabbitRepo.PublishAsync("predictResetAlerts", new BackendIntListMessage { Values = monitorIPIDs });

            }
            catch (Exception e)
            {
                logger.LogError(" Error : failed to publish PredictResetAlerts. Error was :" + e.ToString());
            }
        }

    }
}
