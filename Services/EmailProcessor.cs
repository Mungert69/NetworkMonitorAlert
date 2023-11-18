using System.IO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;
using NetworkMonitor.Objects;
using NetworkMonitor.Alert.Services.Helpers;
using Microsoft.Extensions.Logging;


namespace NetworkMonitor.Alert.Services;

public class EmailProcessor
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

    public EmailProcessor(SystemParams systemParams, ILogger logger)
    {
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
        var report=hostReport.Report;
        var user=hostReport.User;
        string? template = null;
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

        var contentMap = new Dictionary<string, string>
            {
                { "EmailTitle", "Weekly Free Network Monitor Host Report" },
                { "MainContent", report },
                // Add other key-value pairs as needed based on your template
            };
        var urls = GetUrls(user.UserID, user.Email);



        var populatedTemplate = PopulateTemplate(template, contentMap);

        result=await SendTemplate(user.UserID, user.Email, "Weekly Host Report", populatedTemplate, urls);
        return result;
    }


public async Task<List<ResultObj>> UserHostExpire(List<UserInfo> userInfos)
{
    var results = new List<ResultObj>();
    var result=new ResultObj();
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

    foreach (var user in userInfos)
    {
        var urls = GetUrls(user.UserID, user.Email);
        var contentMap = new Dictionary<string, string>
            {
                 { "EmailTitle", "Action Required: Your Free Network Monitor Account" },
                { "HeaderImageUrl", "https://freenetworkmonitor.click/img/logo.jpg" }, // Assuming this is your logo URL
                { "HeaderImageAlt", "Free Network Monitor Logo" },
                 { "MainHeading", "We Miss You at Free Network Monitor!" },
                  { "MainContent", "Hello! We've noticed that you haven't logged in for a while. To keep our services efficient, we've paused the monitoring of your hosts. Don't worry, you can easily resume monitoring by logging back in. Remember, active monitoring is key to staying informed! <br><br>Prefer not to log in every three months? Upgrade to our standard plan, only $1 a month, for uninterrupted monitoring and many additional features." },
                  { "ButtonUrl", "https://freenetworkmonitor.click/subscription" },
                 { "ButtonText", "Reactivate My Hosts" },
                  { "CurrentYear", DateTime.Now.Year.ToString() },
                  { "UnsubscribeUrl", urls.unsubscribeUrl }
            };


        var populatedTemplate = PopulateTemplate(template, contentMap);

        results.Add(await SendTemplate(user.UserID, user.Email, "Important Update: Keep Your Hosts Active with Free Network Monitor", populatedTemplate, urls));
    }
    return results;

}

}