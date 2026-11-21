using System.Text.Json;
using Domain.Entities;
using Infrastructure.Persistence;
namespace Api.Services;

public static class Workflow
{
    /// Verified/Escalated -> Assigned once shelter/resource/volunteer help is allocated.
    public static void MarkAssigned(AppDbContext db, AssistanceRequest req, int userId, string note)
    {
        if (req.Status is RequestStatus.Verified or RequestStatus.Escalated)
        {
            var from = req.Status.ToString();
            req.ChangeStatus(RequestStatus.Assigned);
            db.RequestHistory.Add(new RequestHistory { RequestId = req.RequestId, FromStatus = from, ToStatus = "Assigned", ChangedBy = userId, Note = note });
        }
    }

    /// Queue a citizen notification via the outbox (processed asynchronously, de-duplicated by idempotency key).
    public static void Notify(AppDbContext db, long requestId, string template, string subject, string body) =>
        db.OutboxMessages.Add(new OutboxMessage
        {
            EventType = "CitizenNotice",
            Payload = JsonSerializer.Serialize(new { RequestId = requestId, Template = template, Subject = subject, Body = body })
        });

    public static void Audit(AppDbContext db, int? userId, string action, string entityType, string entityId, string? oldV = null, string? newV = null) =>
        db.AuditEvents.Add(new AuditEvent { UserId = userId, Action = action, EntityType = entityType, EntityId = entityId, OldValue = oldV, NewValue = newV });
}
