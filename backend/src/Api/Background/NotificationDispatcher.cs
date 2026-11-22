using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Api.Background;

/// Sends pending notifications with retry + exponential backoff. After 5 failures a notification becomes a DeadLetter
/// (visible at GET /api/notifications/dead-letters). A failing email provider never blocks the rest of the system.
/// Sender comes from a Factory (SMTP when configured, otherwise log). This worker can be moved to the separate
/// Notification Service project without changing the logic.
public class NotificationDispatcher : BackgroundService
{
    private const int MaxRetries = 5;
    private readonly IServiceScopeFactory _scopes; private readonly ILogger<NotificationDispatcher> _log;
    public NotificationDispatcher(IServiceScopeFactory scopes, ILogger<NotificationDispatcher> log) { _scopes = scopes; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await RunOnce(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { _log.LogError(ex, "Notification dispatcher failed"); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunOnce(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var factory = scope.ServiceProvider.GetRequiredService<INotificationSenderFactory>();
        var now = DateTime.UtcNow;

        var due = await db.Notifications
            .Where(n => (n.Status == "Pending" || n.Status == "Failed") && n.RetryCount < MaxRetries && (n.NextRetryAt == null || n.NextRetryAt <= now))
            .OrderBy(n => n.CreatedAt).Take(20).ToListAsync(ct);

        foreach (var n in due)
        {
            var sender = factory.Create(n.Channel);
            try
            {
                var u = await db.Users.Where(x => x.UserId == n.UserId).Select(x => new { x.Email, x.Phone }).FirstAsync(ct);
                var to = n.Channel == "Sms" ? u.Phone : u.Email;
                if (string.IsNullOrWhiteSpace(to)) throw new InvalidOperationException($"User has no {(n.Channel == "Sms" ? "phone number" : "email")}.");
                if (n.Channel != "Sms" && to.EndsWith("@phone.invalid", StringComparison.OrdinalIgnoreCase))   // phone-only account: no real email to send to
                { n.Status = "Sent"; n.SentAt = DateTime.UtcNow; continue; }
                await sender.SendAsync(to, n.Subject ?? "Relief update", n.Body, ct);
                n.Status = "Sent"; n.SentAt = DateTime.UtcNow;
                db.NotificationAttempts.Add(new NotificationAttempt { NotificationId = n.NotificationId, Provider = sender.Provider, Success = true });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                n.RetryCount++;
                n.Status = n.RetryCount >= MaxRetries ? "DeadLetter" : "Failed";
                n.NextRetryAt = DateTime.UtcNow.AddSeconds(30 * Math.Pow(2, n.RetryCount));   // 1m, 2m, 4m, 8m ...
                db.NotificationAttempts.Add(new NotificationAttempt { NotificationId = n.NotificationId, Provider = sender.Provider, Success = false, ErrorMessage = ex.Message.Length > 900 ? ex.Message[..900] : ex.Message });
                _log.LogWarning(ex, "Notification {Id} failed (attempt {N})", n.NotificationId, n.RetryCount);
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
