using System;
using System.Collections.Generic;
using System.Linq;
using NetworkMonitor.Objects.ServiceMessage;
using System.Diagnostics;
using MetroLog;

namespace NetworkMonitor.Objects.Repository
{
    public class PublishAlertsRepo
    {
        public static void ProcessorAlertSent(ILogger logger, IRabbitRepo rabbitRepo, List<MonitorStatusAlert> publishAlertSentList, List<ProcessorObj> processorList)
        {
            try
            {
                foreach (var processorObj in processorList)
                {
                    List<int> monitorStatusAlertIDs = publishAlertSentList.Where(w => w.AppID == processorObj.AppID).Select(s => s.ID).ToList();
                    if (monitorStatusAlertIDs.Count != 0)
                    {
                        rabbitRepo.Publish<List<int>>("processorAlertSent" + processorObj.AppID, monitorStatusAlertIDs);
                        logger.Info("Sent event processorAlertSent for AppID  " + processorObj.AppID);
                    }
                }
            }
            catch (Exception e)
            {
                logger.Fatal("Error : Unable to send processorAlertSent message. Error was :  " + e.Message.ToString());
            }
        }
        public static void ProcessorAlertFlag(ILogger logger,  IRabbitRepo rabbitRepo, List<MonitorStatusAlert> publishAlertFlagList, List<ProcessorObj> processorList)
        {
            foreach (var processorObj in processorList)
            {
                List<int> monitorStatusAlertIDs = publishAlertFlagList.Where(w => w.AppID == processorObj.AppID).Select(s => s.ID).ToList();
                if (monitorStatusAlertIDs.Count != 0)
                {
                    rabbitRepo.Publish<List<int>>( "processorAlertFlag" + processorObj.AppID, monitorStatusAlertIDs);
                    logger.Info("Sent event processorAlertFlag for AppID  " + processorObj.AppID);
                }
                publishAlertFlagList.Where(w => w.AppID == processorObj.AppID).ToList().ForEach(f => f.AlertFlag=true);
            }
        }
        public static void ProcessorResetAlerts(ILogger logger,  IRabbitRepo rabbitRepo, Dictionary<string, List<int>> monitorIPDic)
        {
            try
            {
                foreach (KeyValuePair<string, List<int>> kvp in monitorIPDic)
                {
                    var monitorIPIDs = new List<int>(kvp.Value);
                    // Dont publish this at the moment as its causing alerts to refire.
                    rabbitRepo.Publish<List<int>>( "processorResetAlerts" + kvp.Key, monitorIPIDs);
                }
            }
            catch (Exception e)
            {
                logger.Error(" Error : failed to publish ProcessResetAlerts. Error was :" + e.ToString());
            }
        }
    }
}