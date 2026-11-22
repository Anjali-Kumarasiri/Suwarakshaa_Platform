using System.Net;
using System.Net.Mail;
using Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
namespace Infrastructure.External;

/// Used when no real provider is configured (development / demo). Writes to the log instead.
public class LogSender : INotificationSender
{
    private readonly ILogger _log; private readonly string _name;
    public LogSender(ILogger log, string name) { _log = log; _name = name; }
    public string Provider => _name;
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    { _log.LogInformation("[{Provider}] to={To} subject={Subject}", _name, to, subject); return Task.CompletedTask; }
}

public class SmtpEmailSender : INotificationSender
{
    private readonly IConfiguration _cfg;
    public SmtpEmailSender(IConfiguration cfg) => _cfg = cfg;
    public string Provider => "smtp";
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        using var client = new SmtpClient(_cfg["Smtp:Host"], int.Parse(_cfg["Smtp:Port"] ?? "587"))
        {
            EnableSsl = bool.Parse(_cfg["Smtp:EnableSsl"] ?? "true"),
            Credentials = new NetworkCredential(_cfg["Smtp:User"], _cfg["Smtp:Password"]),
            Timeout = 8000
        };
        using var msg = new MailMessage(_cfg["Smtp:From"] ?? "noreply@relieflink.local", to, subject, body);
        await client.SendMailAsync(msg, ct);
    }
}

public class NotificationSenderFactory : INotificationSenderFactory
{
    private readonly IConfiguration _cfg; private readonly ILoggerFactory _lf;
    public NotificationSenderFactory(IConfiguration cfg, ILoggerFactory lf) { _cfg = cfg; _lf = lf; }

    public INotificationSender Create(string channel) => channel switch
    {
        "Email" when !string.IsNullOrWhiteSpace(_cfg["Smtp:Host"]) => new SmtpEmailSender(_cfg),
        "Email" => new LogSender(_lf.CreateLogger("EmailLog"), "email-log"),
        "Sms"   => new LogSender(_lf.CreateLogger("SmsLog"), "sms-stub"),     // plug a real SMS provider here
        _       => new LogSender(_lf.CreateLogger("InApp"), "in-app")
    };
}
