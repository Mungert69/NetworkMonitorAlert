using NetworkMonitor.Objects;
using System.Collections.Generic;


namespace NetworkMonitor.Alert.Services;
public interface IAlertProcess
{
    string PublishPrefix { get; set; }
    List<IAlertable> Alerts { get; set; }
    List<AlertMessage> AlertMessages { get ; set ; }
    List<IAlertable> UpdateAlertSentList { get ; set; }
    int AlertThreshold { get ; set ; }
bool Awake { get ; set ; }
    bool Alert { get; set ; }
    bool IsAlertRunning { get; set ; }
    bool PublishProcessor { get ; set ; }
    bool PublishPredict { get; set ; }
    bool PublishScheduler { get; set; }

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
    private List<AlertMessage> _alertMessages = new List<AlertMessage>();
    List<IAlertable> _updateAlertSentList = new List<IAlertable>();

    public string PublishPrefix { get => _publishPrefix; set => _publishPrefix = value; }
    public List<IAlertable> Alerts { get => _alerts; set => _alerts = value; }
    public List<AlertMessage> AlertMessages { get => _alertMessages; set => _alertMessages = value; }
    public List<IAlertable> UpdateAlertSentList { get => _updateAlertSentList; set => _updateAlertSentList = value; }
    public bool Awake { get => _awake; set => _awake = value; }
    public bool Alert { get => Alert1; set => Alert1 = value; }
    public bool Alert1 { get => _alert; set => _alert = value; }
    public int AlertThreshold { get => _alertThreshold; set => _alertThreshold = value; }
    public bool PublishProcessor { get => _publishProcessor; set => _publishProcessor = value; }
    public bool PublishPredict { get => _publishPredict; set => _publishPredict = value; }
    public bool PublishScheduler { get => _publishScheduler; set => _publishScheduler = value; }
    public bool IsAlertRunning { get => _isAlertRunning; set => _isAlertRunning = value; }
}