var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<NotificationWorker>();
builder.Build().Run();

public class NotificationWorker : BackgroundService
{
    private readonly ILogger<NotificationWorker> _log;
    public NotificationWorker(ILogger<NotificationWorker> log) => _log = log;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // TODO (Member 4):
        //  1. Connect to RabbitMQ (queue 'notifications'); the Core API outbox dispatcher publishes events here.
        //  2. For each message: IdempotencyKey = $"{eventId}:{userId}:{channel}".
        //     If it already exists in Notifications table (UNIQUE) -> ACK and skip (no duplicate notification).
        //  3. Send email (SendGrid/SMTP) via a sender chosen by a Factory (Email/Sms/Push).
        //  4. On failure: record NotificationAttempts, RetryCount++, exponential backoff, after N tries -> DeadLetter.
        while (!ct.IsCancellationRequested)
        {
            _log.LogInformation("Notification worker alive");
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }
}
