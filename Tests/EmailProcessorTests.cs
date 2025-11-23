using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using MimeKit;
using NetworkMonitor.Objects;
using Xunit;
using EmailUrls = NetworkMonitor.Alert.Services.EmailProcessor.EmailUrls;
using NetworkMonitor.Alert.Services;

namespace NetworkMonitor.Alert.Tests;

public class EmailProcessorTests
{
    private static EmailProcessor CreateProcessor(bool disableEmails = false)
    {
        var systemParams = new SystemParams
        {
            EmailEncryptKey = "encrypt-key",
            SystemEmail = "reports@example.com",
            SystemUser = "smtp-user",
            SystemPassword = "smtp-password",
            MailServer = "smtp.example.com",
            MailServerPort = 25,
            MailServerUseSSL = false,
            TrustPilotReviewEmail = "trustpilot@example.com",
            ThisSystemUrl = new SystemUrl { ExternalUrl = "https://sender.example.com" },
            PublicIPAddress = "127.0.0.1",
            EmailSendServerName = "https://sender.example.com",
            SendTrustPilot = false
        };

        var logger = new Mock<ILogger>().Object;
        return new EmailProcessor(systemParams, logger, disableEmails);
    }

    private static EmailUrls CreateUrls() =>
        new EmailUrls("https://example.com/resub", "https://example.com/unsub", "encEmail", "encUser", "https://example.com/verify", true, string.Empty);

    [Fact]
    public void CreateMimeMessage_UsesBase64AndUtf8ForHtml()
    {
        var processor = CreateProcessor();
        var urls = CreateUrls();
        var body = "<p style=\"color: #6239AB;\">Speed &#9889; equals sign =&gt; stays intact</p>";

        var message = processor.CreateMimeMessage("user@example.com", "Weekly Report", body, urls, isBodyHtml: true);

        Assert.Equal("Weekly Report", message.Subject);
        Assert.Contains(urls.UnsubscribeUrl, message.Headers["List-Unsubscribe"]);

        var textPart = Assert.IsType<MimeKit.TextPart>(message.Body);
        Assert.Equal("text/html", textPart.ContentType.MimeType);
        Assert.Equal(ContentEncoding.Base64, textPart.ContentTransferEncoding);
        Assert.Equal("utf-8", textPart.ContentType.Charset, ignoreCase: true);
        Assert.Equal(body, textPart.Text);

        using var stream = new MemoryStream();
        message.WriteTo(stream);
        var raw = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("Content-Transfer-Encoding: base64", raw);
        Assert.DoesNotContain("style=3D", raw);
    }

    [Fact]
    public void CreateMimeMessage_WhenPlainTextStillBase64Encoded()
    {
        var processor = CreateProcessor();
        var urls = CreateUrls();
        var body = "Plain text with equals = and unicode ⚡.";

        var message = processor.CreateMimeMessage("user@example.com", "Plain Email", body, urls, isBodyHtml: false);

        var textPart = Assert.IsType<MimeKit.TextPart>(message.Body);
        Assert.Equal("text/plain", textPart.ContentType.MimeType);
        Assert.Equal(body, textPart.Text);
        Assert.Equal(ContentEncoding.Base64, textPart.ContentTransferEncoding);
        Assert.Equal("utf-8", textPart.ContentType.Charset, ignoreCase: true);
    }
}
