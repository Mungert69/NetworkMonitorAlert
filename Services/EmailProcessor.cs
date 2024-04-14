using System.IO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using MailKit.Net.Smtp;
using MimeKit;
using NetworkMonitor.Objects;
using NetworkMonitor.Utils;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace NetworkMonitor.Alert.Services;
public interface IEmailProcessor
{
    bool SendTrustPilot { get; set; }

    Task<ResultObj> SendAlert(AlertMessage alertMessage);
    Task<ResultObj> SendHostReport(HostReportObj hostReport);
    Task<ResultObj> SendGenericEmail(GenericEmailObj emailObj);
    Task<List<ResultObj>> UserHostExpire(List<GenericEmailObj> emailObjs);
    Task<List<ResultObj>> UpgradeAccounts(List<GenericEmailObj> emailObjs);
    bool VerifyEmail(UserInfo userInfo, IAlertable monitorStatusAlert);
}
public class EmailProcessor : IEmailProcessor
{
    private string _emailEncryptKey;
    private string _systemEmail;
    private string _systemUrl;
    private string _systemPassword;
    private string _systemUser;
    private string _trustPilotReviewEmail;
    private string _mailServer;
    private int _mailServerPort;
    private bool _mailServerUseSSL;
    private string _emailSendServerName;
    private SystemUrl _thisSystemUrl;
    private string _publicIPAddress;
    private SpamFilter _spamFilter;
    private ILogger _logger;
    private bool _disableEmailAlert;
    private bool _sendTrustPilot;

    public bool SendTrustPilot { get => _sendTrustPilot; set => _sendTrustPilot = value; }

    public EmailProcessor(SystemParams systemParams, ILogger logger, bool disableEmailAlert)
    {
        _disableEmailAlert = disableEmailAlert;
        _emailEncryptKey = systemParams.EmailEncryptKey;
        _systemEmail = systemParams.SystemEmail;
        _systemUser = systemParams.SystemUser;
        _systemPassword = systemParams.SystemPassword;
        _mailServer = systemParams.MailServer;
        _mailServerPort = systemParams.MailServerPort;
        _mailServerUseSSL = systemParams.MailServerUseSSL;
        _emailSendServerName = systemParams.EmailSendServerName;
        _trustPilotReviewEmail = systemParams.TrustPilotReviewEmail;
        _thisSystemUrl = systemParams.ThisSystemUrl;
        _publicIPAddress = systemParams.PublicIPAddress;
        _sendTrustPilot = systemParams.SendTrustPilot;
        _spamFilter = new SpamFilter(logger);
        _logger = logger;
    }



    public string PopulateTemplate(string template, Dictionary<string, string> contentMap)
    {
        foreach (var item in contentMap)
        {
            template = template.Replace($"{{{item.Key}}}", item.Value);
        }
        return template;
    }

    public async Task<ResultObj> SendAlert(AlertMessage alertMessage)
    {
        ResultObj result = new ResultObj();
        if (_disableEmailAlert)
        {
            result.Success = false;
            result.Message += " Error : Emails are disabled in appsettings.json (DisableEmailAlert=true) . ";
            return result;
        }
        if (alertMessage.UserInfo == null || alertMessage.UserInfo.UserID == null)
        {
            result.Message = " Error : Missing UserInfo ";
            result.Success = false;
            return result;
        }



        var urls = GetUrls(alertMessage.UserInfo.UserID, alertMessage.UserInfo.Email);
        if (alertMessage.VerifyLink)
        {
            result = _spamFilter.IsVerifyLimit(alertMessage.UserInfo.UserID);
            if (!result.Success)
            {
                return result;
            }
            string verifyUrl = _emailSendServerName + "/email/verifyemail?email=" + urls.encryptEmailAddressStr + "&userid=" + urls.encryptUserID;
            alertMessage.Message += "\n\nPlease click on this link to verify your email " + verifyUrl;
        }
        alertMessage.Message += "\n\nThis message was sent by the messenger running at " + _emailSendServerName + " (" + _publicIPAddress.ToString() + ")\n\n To unsubscribe from receiving these messages, please click this link " + urls.unsubscribeUrl + "\n\n To re-subscribe to receiving these messages, please click this link " + urls.resubscribeUrl;
        string emailFrom = _systemEmail;
        string systemPassword = _systemPassword;
        string systemUser = _systemUser;
        int mailServerPort = _mailServerPort;
        bool mailServerUseSSL = _mailServerUseSSL;
        try
        {
            MimeMessage message = new MimeMessage();
            message.Headers.Add("List-Unsubscribe", "<" + urls.unsubscribeUrl + ">, <mailto:" + emailFrom + "?subject=unsubscribe>");
            MailboxAddress from = new MailboxAddress("Free Network Monitor",
            emailFrom);
            message.From.Add(from);
            if (alertMessage.SendTrustPilot)
            {
                MailboxAddress bcc = new MailboxAddress("Trust Pilot",
         _trustPilotReviewEmail);
                message.Bcc.Add(bcc);
            }
            MailboxAddress to = new MailboxAddress(alertMessage.Name,
            alertMessage.EmailTo);
            message.To.Add(to);
            //message.Subject = "Network Monitor Alert : Host Down";
            message.Subject = alertMessage.Subject;
            BodyBuilder bodyBuilder = new BodyBuilder();
            bodyBuilder.TextBody = alertMessage.Message;
            //bodyBuilder.Attachments.Add(_env.WebRootPath + "\\file.png");
            message.Body = bodyBuilder.ToMessageBody();
            SmtpClient client = new SmtpClient();
            client.ServerCertificateValidationCallback = (mysender, certificate, chain, sslPolicyErrors) => { return true; };
            client.CheckCertificateRevocation = false;
            if (mailServerUseSSL)
            {
                await client.ConnectAsync(_mailServer, mailServerPort, true);
            }
            else
            {
                await client.ConnectAsync(_mailServer, mailServerPort, MailKit.Security.SecureSocketOptions.StartTls);
            }
            client.Authenticate(systemUser, systemPassword);
            client.Send(message);
            client.Disconnect(true);
            client.Dispose();
            result.Message = "Email with subject " + alertMessage.Subject + " sent ok";
            result.Success = true;
            _spamFilter.UpdateAlertSentList(alertMessage);
            _logger.LogInformation(result.Message);
        }
        catch (Exception e)
        {
            result.Message = "Email with subject " + alertMessage.Subject + " failed to send . Error was :" + e.Message.ToString().ToString();
            result.Success = false;
            _logger.LogError(result.Message);
        }
        return result;
    }

    private (string resubscribeUrl, string unsubscribeUrl, string encryptEmailAddressStr, string encryptUserID) GetUrls(string userId, string email)
    {
        string encryptEmailAddressStr = EncryptionHelper.EncryptStr(_emailEncryptKey, email);
        string encryptUserID = EncryptionHelper.EncryptStr(_emailEncryptKey, userId);
        string subscribeUrl = _emailSendServerName + "/email/unsubscribe?email=" + encryptEmailAddressStr + "&userid=" + encryptUserID;
        string resubscribeUrl = subscribeUrl + "&subscribe=true";
        string unsubscribeUrl = subscribeUrl + "&subscribe=false";
        return (resubscribeUrl, unsubscribeUrl, encryptEmailAddressStr, encryptUserID);
    }
    private async Task<ResultObj> SendTemplate(string userId, string email, string subject, string body, (string resubscribeUrl, string unsubscribeUrl, string encryptEmailAddressStr, string encryptUserID
     ) urls, bool isBodyHtml = true)
    {
        ResultObj result = new ResultObj();
        string systemPassword = _systemPassword;
        string systemUser = _systemUser;
        int mailServerPort = _mailServerPort;
        bool mailServerUseSSL = _mailServerUseSSL;

        if (_disableEmailAlert)
        {
            result.Success = false;
            result.Message += " Error : Emails are disabled in appsettings.json (DisableEmailAlert=true) . ";
            return result;
        }
        try
        {
            var message = new MimeMessage();

            message.Headers.Add("List-Unsubscribe", "<" + urls.unsubscribeUrl + ">, <mailto:" + _systemEmail + "?subject=unsubscribe>");

            message.From.Add(new MailboxAddress("Free Network Monitor", _systemEmail));
            message.To.Add(new MailboxAddress("", email));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder();
            if (isBodyHtml)
            {
                bodyBuilder.HtmlBody = body;
            }
            else
            {
                bodyBuilder.TextBody = body;
            }

            message.Body = bodyBuilder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                client.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
                client.CheckCertificateRevocation = false;

                if (mailServerUseSSL)
                {
                    await client.ConnectAsync(_mailServer, mailServerPort, true);
                }
                else
                {
                    await client.ConnectAsync(_mailServer, mailServerPort, MailKit.Security.SecureSocketOptions.StartTls);
                }

                client.Authenticate(systemUser, systemPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }

            result.Message = "Email sent successfully to " + email;
            result.Success = true;
        }
        catch (Exception e)
        {
            result.Message = "Failed to send email to " + email + ". Error: " + e.Message;
            result.Success = false;
        }

        return result;
    }

    public async Task<ResultObj> SendHostReport(HostReportObj hostReport)
    {
        var result = new ResultObj();
        var report = hostReport.Report;
        var user = hostReport.UserInfo;
        var headerImageUrl = BuildUrl(hostReport);
        string? template = null;
        if (_disableEmailAlert)
        {
            result.Success = false;
            result.Message += " Error : Emails are disabled in appsettings.json (DisableEmailAlert=true) . ";
            return result;
        }
        try
        {
            template = File.ReadAllText("./templates/user-message-template.html");
        }
        catch (Exception e)
        {

            result.Success = false;
            result.Message = $" Error : Could not open file /templates/user-message-template.html : Error was : {e.Message} .";
            return result;
        }
        if (template == null)
        {
            result.Success = false;
            result.Message = " Error : file ./templates/user-message-template.html returns null .";
            return result;
        }
        if (user.DisableEmail)
        {
            result.Success = false;
            result.Message = $" Warning  : User has disabled email {user.UserID} .";
            return result;
        }
        var urls = GetUrls(user.UserID, user.Email);
        var contentMap = new Dictionary<string, string>
            {
                { "EmailTitle", "Weekly Free Network Monitor Host Report" },
                 { "HeaderImageUrl",  headerImageUrl}, // Assuming this is your logo URL
                { "HeaderImageAlt", "Free Network Monitor Logo" },
              { "MainHeading", "Free Network Monitor Host Report" },
                  { "ButtonUrl", "https://freenetworkmonitor.click/dashboard" },
                 { "ButtonText", "Login and View My Hosts" },
                  { "CurrentYear", DateTime.Now.Year.ToString() },
                { "MainContent", report },
                                  { "UnsubscribeUrl", urls.unsubscribeUrl }
                // Add other key-value pairs as needed based on your template
            };




        var populatedTemplate = PopulateTemplate(template, contentMap);

        result = await SendTemplate(user.UserID, user.Email, contentMap["EmailTitle"], populatedTemplate, urls);
        return result;
    }

    public async Task<ResultObj> SendGenericEmail(GenericEmailObj emailObj)
    {
        var result = new ResultObj();

        string? template = null;
        if (_disableEmailAlert)
        {
            result.Success = false;
            result.Message += " Error : Emails are disabled in appsettings.json (DisableEmailAlert=true) . ";
            return result;
        }
        try
        {
            template = File.ReadAllText("./templates/user-message-template.html");
        }
        catch (Exception e)
        {
            result.Success = false;
            result.Message = $"Error: Could not open file /templates/user-message-template.html: {e.Message}";
            return result;
        }

        if (template == null)
        {
            result.Success = false;
            result.Message = "Error: file ./templates/user-message-template.html returns null.";
            return result;
        }
        if (emailObj.UserInfo.DisableEmail)
        {
            result.Success = false;
            result.Message = $" Warning  : User has disabled email {emailObj.UserInfo.UserID} .";
            return result;
        }

        var urls = GetUrls(emailObj.UserInfo.UserID, emailObj.UserInfo.Email);
        var contentMap = new Dictionary<string, string>
    {
        { "EmailTitle", emailObj.EmailTitle },
        { "HeaderImageUrl", BuildUrl(emailObj as IGenericEmailObj)},
        { "HeaderImageAlt", emailObj.HeaderImageAlt },
        { "MainHeading", emailObj.MainHeading },
        { "MainContent", emailObj.MainContent },
        { "ButtonUrl", emailObj.ButtonUrl },
        { "ButtonText", emailObj.ButtonText},
        { "CurrentYear", emailObj.CurrentYear },
        { "UnsubscribeUrl", urls.unsubscribeUrl }
    };

        var populatedTemplate = PopulateTemplate(template, contentMap);

        result = await SendTemplate(emailObj.UserInfo.UserID, emailObj.UserInfo.Email, emailObj.EmailTitle, populatedTemplate, urls);
        return result;
    }

    public async Task<List<ResultObj>> UserHostExpire(List<GenericEmailObj> emailObjs)
    {
        var results = new List<ResultObj>();
        var result = new ResultObj();
        string? template = null;
        if (_disableEmailAlert)
        {
            result.Success = false;
            result.Message += " Error : Emails are disabled in appsettings.json (DisableEmailAlert=true) . ";
            results.Add(result);
            return results;
        }
        try
        {
            template = File.ReadAllText("./templates/user-message-template.html");
        }
        catch (Exception e)
        {

            result.Success = false;
            result.Message = $" Error : Could not open file /templates/user-message-template.html : Error was : {e.Message} .";
            results.Add(result);
            return results;
        }
        if (template == null)
        {
            result.Success = false;
            result.Message = " Error : file ./templates/user-message-template.html returns null .";
            results.Add(result);
            return results;
        }

        foreach (var emailObj in emailObjs)
        {
            if (emailObj.UserInfo.DisableEmail)
            {
                results.Add(new ResultObj { Success = false, Message = $" Warning : User Email Disabled {emailObj.UserInfo.UserID} ." });
            }
            else
            {
                var urls = GetUrls(emailObj.UserInfo.UserID, emailObj.UserInfo.Email);
                var contentMap = new Dictionary<string, string>
            {
                 { "EmailTitle", "Action Required: Your Free Network Monitor Account"},
                { "HeaderImageUrl",  BuildUrl(emailObj as IGenericEmailObj)},
                { "HeaderImageAlt", emailObj.HeaderImageAlt },
                 { "MainHeading", "We Miss You at Free Network Monitor!" },
                  {"MainContent", $"Hello! We've noticed a lapse in your recent login activity, which has led to a temporary pause in the monitoring of your hosts to optimize our resource usage : {emailObj.ExtraMessage}. As someone dedicated to maintaining uptime, especially for your customer-facing services, we recognize the importance of continuous monitoring to keep your systems secure and efficient. To ensure your services remain operational and your customers satisfied, please log in as soon as possible to reactivate monitoring. Active engagement is crucial for seamless operations and maintaining customer trust!"},
                  { "ButtonUrl", "https://freenetworkmonitor.click/dashboard" },
                 { "ButtonText", "Reactivate My Hosts" },
                  { "CurrentYear", emailObj.CurrentYear },
                  { "UnsubscribeUrl", urls.unsubscribeUrl }
            };


                var populatedTemplate = PopulateTemplate(template, contentMap);
                // Dont send to fast.
                await Task.Delay(5000);
                results.Add(await SendTemplate(emailObj.UserInfo.UserID, emailObj.UserInfo.Email, contentMap["EmailTitle"], populatedTemplate, urls));

            }
        }
        return results;

    }

    public async Task<List<ResultObj>> UpgradeAccounts(List<GenericEmailObj> emailObjs)
    {
        var results = new List<ResultObj>();
        var result = new ResultObj();
        string? template = null;
        try
        {
            template = File.ReadAllText("./templates/user-message-template.html");
        }
        catch (Exception e)
        {

            result.Success = false;
            result.Message = $" Error : Could not open file /templates/user-message-template.html : Error was : {e.Message} .";
            results.Add(result);
            return results;
        }
        if (template == null)
        {
            result.Success = false;
            result.Message = " Error : file ./templates/user-message-template.html returns null .";
            results.Add(result);
            return results;
        }

        foreach (var emailObj in emailObjs)
        {
            if (emailObj.UserInfo.DisableEmail)
            {
                results.Add(new ResultObj { Success = false, Message = $" Warning : User Email Disabled {emailObj.UserInfo.UserID} ." });
            }
            else
            {
                var urls = GetUrls(emailObj.UserInfo.UserID, emailObj.UserInfo.Email);
                var contentMap = new Dictionary<string, string>
            {
                 { "EmailTitle", "Complimentary 6-Month Standard Plan Upgrade from Free Network Monitor"},
                { "HeaderImageUrl",  BuildUrl(emailObj as IGenericEmailObj)},
                { "HeaderImageAlt", emailObj.HeaderImageAlt },
                 { "MainHeading", "Our Apology and a Special Offer from Free Network Monitor" },
                  { "MainContent", "<p>We've noticed an issue where our reminder email for inactive accounts failed to send. Consequently, the monitoring of your hosts was inadvertently paused, and we deeply apologize for any inconvenience this may have caused.</p><p>To rectify this, we're offering you a complimentary upgrade to our Standard Plan for 6 months. This upgrade includes:</p><ul><li>Monitoring for up to 50 hosts</li><li>Enhanced capabilities: ICMP, Http, Dns, Raw Connect, Smtp Ping, and Quantum Ready checks</li><li>Email support</li><li>6-month full response data retention</li></ul><p>To take advantage of this upgrade and resume monitoring, please log in to your account and re-enable the hosts you wish to monitor. You can also add new hosts to fully utilize the features of the Standard Plan.</p><p>We sincerely hope this gesture underscores our commitment to your satisfaction and trust in our services. If you have any questions or require assistance, please feel free to contact us.</p><p>Thank you for your understanding, and we look forward to serving your network monitoring needs.</p><p>Warm regards,</p><p>Mahadeva<br>Tech Support<br>Free Network Monitor Team</p>" },
                  { "ButtonUrl", "https://freenetworkmonitor.click/dashboard" },
                 { "ButtonText", "Use your email Address to Login" },
                  { "CurrentYear", emailObj.CurrentYear },
                  { "UnsubscribeUrl", urls.unsubscribeUrl }
            };


                var populatedTemplate = PopulateTemplate(template, contentMap);
                // Dont send to fast.
                await Task.Delay(5000);
                results.Add(await SendTemplate(emailObj.UserInfo.UserID, emailObj.UserInfo.Email, contentMap["EmailTitle"], populatedTemplate, urls));

            }
        }
        return results;

    }
    private string BuildUrl(IGenericEmailObj genericEmailObj)
    {
        StringBuilder sb = new StringBuilder(genericEmailObj.HeaderImageUri);
        sb.TrimEnd(new char[] { ':', '/' });
        sb.Append("/Email/Logo");
        //sb.Append(genericEmailObj.HeaderImageFile);
        sb.Append("?id=");
        if (genericEmailObj != null && !string.IsNullOrEmpty(genericEmailObj.ID.ToString()))
        {
            sb.Append(EncryptionHelper.EncryptStr(_emailEncryptKey, genericEmailObj.ID.ToString()));
        }
        return sb.ToString();
    }

    public bool VerifyEmail(UserInfo userInfo, IAlertable monitorStatusAlert)
    {
        bool isValid = false;
        // Validate email format

        if (!String.IsNullOrEmpty(monitorStatusAlert.AddUserEmail))
        {

            try
            {
                var mailAddress = new System.Net.Mail.MailAddress(monitorStatusAlert.AddUserEmail);
                isValid = monitorStatusAlert.IsEmailVerified;
                userInfo.Email = monitorStatusAlert.AddUserEmail;
            }
            catch (FormatException)
            {
                _logger.LogWarning(" Warning : Invalid email format: " + monitorStatusAlert.AddUserEmail);
            }

        }
        else
        {
            isValid = userInfo.Email_verified;
        }

        return isValid;
    }

}