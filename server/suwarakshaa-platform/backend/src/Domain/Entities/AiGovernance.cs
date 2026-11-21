namespace Domain.Entities;

public class AiRecommendation
{
    public long RecommendationId { get; set; }
    public string Type { get; set; } = default!;       // Classification, Priority, ResourceMatch ...
    public long? RequestId { get; set; }
    public string ModelName { get; set; } = default!;
    public string OutputJson { get; set; } = default!;  // the ORIGINAL AI recommendation (never edited)
    public decimal Confidence { get; set; }
    public string? Rationale { get; set; }
    public bool IsFallback { get; set; }
    public RecommendationStatus Status { get; set; } = RecommendationStatus.PendingReview;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class HumanDecision
{
    public long DecisionId { get; set; }
    public long RecommendationId { get; set; }
    public DecisionAction Action { get; set; }
    public string? FinalValueJson { get; set; }
    public string? Reason { get; set; }
    public int DecidedBy { get; set; }
    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;
    public bool RequiresSecondApproval { get; set; }
    public int? SecondApprovedBy { get; set; }
    public DateTime? SecondApprovedAt { get; set; }
}

public class AuditEvent
{
    public long AuditId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public int? UserId { get; set; }
    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string EntityId { get; set; } = default!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

public class OutboxMessage
{
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}
