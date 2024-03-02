using NetworkMonitor.Objects;
using System.Collections.Generic;
using System.Linq;


namespace NetworkMonitor.Alert.Services;
public interface IAlertProcess
{
    string PublishPrefix { get; set; }
    List<IAlertable> Alerts { get; set; }
    List<AlertMessage> AlertMessages { get; set; }
    List<IAlertable> UpdateAlertSentList { get; set; }
    int AlertThreshold { get; set; }
    bool Awake { get; set; }
    bool Alert { get; set; }
    bool IsAlertRunning { get; set; }
    bool PublishProcessor { get; set; }
    bool PublishPredict { get; set; }
    bool PublishScheduler { get; set; }
    bool CheckAlerts { get; set; }
    public bool DisableEmailAlert { get; set; }
  bool IsPredictProcess{get ;}
    bool IsMonitorProcess{get ;}

}
public class AlertProcess : IAlertProcess
{
    private List<IAlertable> _alerts = new List<IAlertable>();
    private bool _publishProcessor;
    private bool _publishPredict;
    private bool _publishScheduler;
    private string _publishPrefix = "";
    private bool _awake;
    private bool _alert;
    private bool _isAlertRunning;
    private int _alertThreshold;
    private bool _checkAlerts;
    private bool _disableEmailAlert;
    private List<AlertMessage> _alertMessages = new List<AlertMessage>();
    List<IAlertable> _updateAlertSentList = new List<IAlertable>();

    public string PublishPrefix { get => _publishPrefix; set => _publishPrefix = value; }
    public List<IAlertable> Alerts { get => _alerts; set => _alerts = value; }
    public List<AlertMessage> AlertMessages { get => _alertMessages; set => _alertMessages = value; }
    public List<IAlertable> UpdateAlertSentList { get => _updateAlertSentList; set => _updateAlertSentList = value; }
    public bool Awake { get => _awake; set => _awake = value; }
    public bool Alert { get => _alert; set => _alert = value; }
    public int AlertThreshold { get => _alertThreshold; set => _alertThreshold = value; }
    public bool PublishProcessor { get => _publishProcessor; set => _publishProcessor = value; }
    public bool PublishPredict { get => _publishPredict; set => _publishPredict = value; }
    public bool PublishScheduler { get => _publishScheduler; set => _publishScheduler = value; }
    public bool IsAlertRunning { get => _isAlertRunning; set => _isAlertRunning = value; }
    public bool CheckAlerts { get => _checkAlerts; set => _checkAlerts = value; }
    public bool DisableEmailAlert { get => _disableEmailAlert; set => _disableEmailAlert = value; }
    public bool IsPredictProcess{get => Alerts.Any(a => a is PredictStatusAlert);}
    public bool IsMonitorProcess{get => Alerts.Any(a => a is MonitorStatusAlert);}

}