using System.Text.Json;
using Application.Abstractions;
using Application.Strategies;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Api.Background;

/// Transactional-outbox dispatcher (async, event-driven). Business code writes events in the same DB transaction as
/// its data; this worker handles them later. Failures: RetryCount++, LastError; after 5 tries a message is left for
/// manual inspection (dead letter). Handlers are idempotent so a retry never duplicates work.
public class OutboxProcessor : BackgroundService
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<OutboxProcessor> _log;
    public OutboxProcessor(IServiceScopeFactory scopes, ILogger<OutboxProcessor> log) { _scopes = scopes; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await ProcessBatchAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { _log.LogError(ex, "Outbox batch failed"); }
            try { await Task.Delay(TimeSpan.FromSeconds(3), ct); } catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        List<Guid> ids;
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ids = await db.OutboxMessages.Where(m => m.ProcessedAt == null && m.RetryCount < 5)
                .OrderBy(m => m.CreatedAt).Take(20).Select(m => m.MessageId).ToListAsync(ct);
        }
        foreach (var id in ids) await ProcessOneAsync(id, ct);      // each message gets its own DbContext: one failure cannot affect another
    }

    private async Task ProcessOneAsync(Guid id, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ai = scope.ServiceProvider.GetRequiredService<IAiClient>();
        var m = await db.OutboxMessages.FindAsync(new object[] { id }, ct);
        if (m is null || m.ProcessedAt is not null) return;
        try
        {
            switch (m.EventType)
            {
                case "RequestSubmitted":        await OnRequestSubmitted(db, ai, m, ct); break;
                case "AiRecommendationDecided": await OnDecisionMade(db, m, ct); break;
                case "CitizenNotice":           await OnCitizenNotice(db, m, ct); break;
                case "AlertPublished":          await OnAlertPublished(db, m, ct); break;
                default: _log.LogWarning("Unknown outbox event {Type}", m.EventType); break;
            }
            m.ProcessedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            db.ChangeTracker.Clear();                              // drop half-done changes of this message
            var failed = await db.OutboxMessages.FindAsync(new object[] { id }, ct);
            if (failed is not null)
            {
                failed.RetryCount++;
                failed.LastError = ex.Message.Length > 900 ? ex.Message[..900] : ex.Message;
                await db.SaveChangesAsync(ct);
            }
            _log.LogWarning(ex, "Outbox message {Id} failed", id);
        }
    }

    // RequestSubmitted -> AI classification (pending human review) + duplicate check + confirmation notice.
    private static async Task OnRequestSubmitted(AppDbContext db, IAiClient ai, OutboxMessage m, CancellationToken ct)
    {
        var id = JsonDocument.Parse(m.Payload).RootElement.GetProperty("RequestId").GetInt64();
        var req = await db.AssistanceRequests.FindAsync(new object[] { id }, ct) ?? throw new InvalidOperationException($"Request {id} not found");

        if (!await db.AiRecommendations.AnyAsync(r => r.RequestId == id && r.Type == "Classification", ct))   // idempotent on retry
        {
            var result = await ai.ClassifyAsync(req.Description, ct);
            db.AiRecommendations.Add(new AiRecommendation
            {
                Type = "Classification", RequestId = id,
                ModelName = result.IsFallback ? "rule-based-fallback" : "ai-service",
                OutputJson = JsonSerializer.Serialize(new { category = result.Category, urgency = result.Urgency }, Web),
                Confidence = result.Confidence, Rationale = result.Rationale, IsFallback = result.IsFallback
            });
        }

        // duplicate detection: flag possible duplicates for a human, never delete or merge automatically
        var since = DateTime.UtcNow.AddHours(-24);
        var others = await db.AssistanceRequests
            .Where(r => r.RequestId != id && r.CrisisId == req.CrisisId && r.Category == req.Category && r.CreatedAt >= since)
            .Select(r => new { r.RequestId, r.Description, r.SubmittedBy, r.Latitude, r.Longitude }).Take(200).ToListAsync(ct);
        foreach (var o in others)
        {
            var score = TextSimilarity.Jaccard(req.Description, o.Description);
            bool sameUser = o.SubmittedBy == req.SubmittedBy;
            bool near = req.Latitude is not null && o.Latitude is not null &&
                        GeoMath.HaversineKm((double)req.Latitude, (double)req.Longitude!, (double)o.Latitude, (double)o.Longitude!) < 0.5;
            if (score >= 0.6 || (sameUser && score >= 0.4) || (near && score >= 0.45))
            {
                var a = Math.Min(id, o.RequestId); var b = Math.Max(id, o.RequestId);
                if (!await db.DuplicateCandidates.AnyAsync(d => d.RequestId == a && d.OtherRequestId == b, ct))
                    db.DuplicateCandidates.Add(new DuplicateCandidate { RequestId = a, OtherRequestId = b, SimilarityScore = (decimal)Math.Round(score, 4) });
            }
        }

        // suspicious-activity detection: FLAG for a human (the request is still processed normally)
        var hourAgo = DateTime.UtcNow.AddHours(-1);
        var dayAgo = DateTime.UtcNow.AddHours(-24);
        var userLastHour = await db.AssistanceRequests.CountAsync(r => r.SubmittedBy == req.SubmittedBy && r.CreatedAt >= hourAgo, ct);
        var nearby = 0;
        if (req.Latitude is not null && req.Longitude is not null)
        {
            var lat0 = req.Latitude.Value; var lon0 = req.Longitude.Value;
            nearby = await db.AssistanceRequests.CountAsync(r => r.RequestId != id && r.CreatedAt >= hourAgo && r.Latitude != null && r.Longitude != null
                         && r.Latitude > lat0 - 0.002m && r.Latitude < lat0 + 0.002m && r.Longitude > lon0 - 0.002m && r.Longitude < lon0 + 0.002m, ct);
        }
        var sameText = await db.AssistanceRequests.CountAsync(r => r.RequestId != id && r.Description == req.Description && r.CreatedAt >= dayAgo, ct);
        var anomaly = AnomalyDetector.Evaluate(new AnomalyInput(req.PeopleAffected, req.Description, userLastHour, nearby, sameText));
        if (anomaly.IsSuspicious)
        {
            req.IsFlaggedSuspicious = true;
            db.AuditEvents.Add(new AuditEvent { Action = "ANOMALY_FLAGGED", EntityType = "AssistanceRequest", EntityId = id.ToString(), NewValue = JsonSerializer.Serialize(new { anomaly.Score, anomaly.Reasons }) });
        }

        if (req.Status == RequestStatus.Submitted)
        {
            req.ChangeStatus(RequestStatus.UnderReview);
            db.RequestHistory.Add(new RequestHistory { RequestId = id, FromStatus = "Submitted", ToStatus = "UnderReview", Note = "AI suggestion ready for human review" });
        }
        db.OutboxMessages.Add(new OutboxMessage { EventType = "CitizenNotice", Payload = JsonSerializer.Serialize(new { RequestId = id, Template = "REQUEST_RECEIVED", Subject = $"Request {req.ReferenceNo} received", Body = "We received your request and our team is reviewing it." }) });
    }

    // A human decided -> apply the FINAL value (may differ from the AI's) to the request and tell the citizen.
    private static async Task OnDecisionMade(AppDbContext db, OutboxMessage m, CancellationToken ct)
    {
        var root = JsonDocument.Parse(m.Payload).RootElement;
        var recId = root.GetProperty("RecommendationId").GetInt64();
        var action = root.GetProperty("action").GetString();
        if (!root.TryGetProperty("RequestId", out var ridEl) || ridEl.ValueKind != JsonValueKind.Number) return;

        var req = await db.AssistanceRequests.FindAsync(new object[] { ridEl.GetInt64() }, ct);
        if (req is null) return;
        var decision = await db.HumanDecisions.Where(d => d.RecommendationId == recId).OrderByDescending(d => d.DecidedAt).FirstAsync(ct);

        if (action is "Approve" or "Modify")
        {
            using var doc = JsonDocument.Parse(decision.FinalValueJson ?? "{}");
            if (doc.RootElement.TryGetProperty("urgency", out var u) && u.TryGetInt32(out var urgency)) req.FinalPriority = (byte)Math.Clamp(urgency, 1, 5);
            if (doc.RootElement.TryGetProperty("category", out var c) && c.GetString() is { Length: > 0 } cat) req.Category = cat;

            if (req.DueAt is null && req.FinalPriority is byte pr)                 // service-level target by priority
                req.DueAt = DateTime.UtcNow.AddHours(pr >= 5 ? 2 : pr == 4 ? 6 : pr == 3 ? 24 : 72);

            if (req.Status == RequestStatus.UnderReview)
            {
                var next = req.FinalPriority == 5 ? RequestStatus.Escalated : RequestStatus.Verified;
                db.RequestHistory.Add(new RequestHistory { RequestId = req.RequestId, FromStatus = req.Status.ToString(), ToStatus = next.ToString(), ChangedBy = decision.DecidedBy, Note = $"AI suggestion {action}d by a coordinator" });
                req.ChangeStatus(next);
                db.OutboxMessages.Add(new OutboxMessage { EventType = "CitizenNotice", Payload = JsonSerializer.Serialize(new { RequestId = req.RequestId, Template = "REQUEST_VERIFIED", Subject = "Your request was verified", Body = $"Your request {req.ReferenceNo} has been verified and is being arranged." }) });
            }
        }
        else if (action == "Reject")
        {
            db.RequestHistory.Add(new RequestHistory { RequestId = req.RequestId, FromStatus = req.Status.ToString(), ToStatus = req.Status.ToString(), ChangedBy = decision.DecidedBy, Note = "AI suggestion rejected: manual handling needed" });
        }
    }

    private static readonly HashSet<string> SmsTemplates = new() { "SHELTER_ALLOCATED", "RESOURCE_ALLOCATED", "RESOURCE_DELIVERED" };

    // CitizenNotice -> notification rows (in-app, email and, for key events, SMS) in the citizen's language when a template exists.
    // IdempotencyKey (messageId:user:channel) is UNIQUE, so processing the same event twice can never notify twice.
    private static async Task OnCitizenNotice(AppDbContext db, OutboxMessage m, CancellationToken ct)
    {
        var root = JsonDocument.Parse(m.Payload).RootElement;
        var req = await db.AssistanceRequests.FindAsync(new object[] { root.GetProperty("RequestId").GetInt64() }, ct);
        if (req is null) return;
        var template = root.GetProperty("Template").GetString()!;
        var subject = root.GetProperty("Subject").GetString();
        var body = root.GetProperty("Body").GetString()!;
        var user = await db.Users.FindAsync(new object[] { req.SubmittedBy }, ct);
        if (user is null) return;

        if (Localizer.TryGet(template, user.PreferredLanguage) is { } loc) { subject = loc.Subject; body = loc.Body; }
        subject = subject?.Replace("{ref}", req.ReferenceNo); body = body.Replace("{ref}", req.ReferenceNo);

        var channels = new List<string> { "InApp", "Email" };
        if (SmsTemplates.Contains(template) && !string.IsNullOrWhiteSpace(user.Phone)) channels.Add("Sms");

        foreach (var channel in channels)
        {
            var key = $"{m.MessageId}:{user.UserId}:{channel}";
            if (await db.Notifications.AnyAsync(n => n.IdempotencyKey == key, ct)) continue;
            db.Notifications.Add(new Notification
            {
                UserId = user.UserId, RequestId = req.RequestId, Channel = channel, TemplateCode = template, Language = user.PreferredLanguage,
                Subject = channel == "Sms" ? null : subject, Body = channel == "Sms" && body.Length > 300 ? body[..300] : body, IdempotencyKey = key,
                Status = channel == "InApp" ? "Sent" : "Pending", SentAt = channel == "InApp" ? DateTime.UtcNow : null
            });
        }
    }

    // AlertPublished -> emergency announcement to all active citizens (in-app + email), de-duplicated per user.
    private static async Task OnAlertPublished(AppDbContext db, OutboxMessage m, CancellationToken ct)
    {
        var alertId = JsonDocument.Parse(m.Payload).RootElement.GetProperty("AlertId").GetInt32();
        var alert = await db.PublicAlerts.FindAsync(new object[] { alertId }, ct);
        if (alert is null || !alert.IsActive) return;

        var prefix = m.MessageId + ":";
        var existing = (await db.Notifications.Where(n => n.IdempotencyKey.StartsWith(prefix)).Select(n => n.IdempotencyKey).ToListAsync(ct)).ToHashSet();
        var users = await (from u in db.Users.Where(u => u.IsActive)
                           join ur in db.UserRoles on u.UserId equals ur.UserId
                           join r in db.Roles.Where(r => r.Name == "Citizen") on ur.RoleId equals r.RoleId
                           select new { u.UserId, u.PreferredLanguage }).Take(5000).ToListAsync(ct);
        foreach (var u in users)
            foreach (var channel in new[] { "InApp", "Email" })
            {
                var key = $"{m.MessageId}:{u.UserId}:{channel}";
                if (existing.Contains(key)) continue;
                db.Notifications.Add(new Notification
                {
                    UserId = u.UserId, Channel = channel, TemplateCode = "ALERT", Language = alert.Language, Subject = $"[{alert.Severity}] {alert.Title}", Body = alert.Body,
                    IdempotencyKey = key, Status = channel == "InApp" ? "Sent" : "Pending", SentAt = channel == "InApp" ? DateTime.UtcNow : null
                });
            }
    }
}
