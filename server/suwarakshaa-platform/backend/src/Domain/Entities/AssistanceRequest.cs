namespace Domain.Entities;

public class AssistanceRequest
{
    public long RequestId { get; set; }
    public string ReferenceNo { get; set; } = default!;
    public int CrisisId { get; set; }
    public int SubmittedBy { get; set; }
    public int? AreaId { get; set; }
    public int? HouseholdId { get; set; }
    public int? AssignedOrgId { get; set; }
    public int? AssignedTo { get; set; }
    public DateTime? DueAt { get; set; }
    public string Category { get; set; } = default!;
    public string Description { get; set; } = default!;
    public int PeopleAffected { get; set; } = 1;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? FormattedAddress { get; set; }
    public byte? UserUrgency { get; set; }
    public byte? FinalPriority { get; set; }          // set only by a human decision
    public RequestStatus Status { get; private set; } = RequestStatus.Submitted;
    public bool IsFlaggedSuspicious { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    private static readonly Dictionary<RequestStatus, RequestStatus[]> Allowed = new()
    {
        [RequestStatus.Submitted]   = new[] { RequestStatus.UnderReview, RequestStatus.Rejected, RequestStatus.Merged },
        [RequestStatus.UnderReview] = new[] { RequestStatus.Verified, RequestStatus.Escalated, RequestStatus.Rejected, RequestStatus.Merged },
        [RequestStatus.Verified]    = new[] { RequestStatus.Assigned, RequestStatus.Escalated },
        [RequestStatus.Escalated]   = new[] { RequestStatus.Assigned },
        [RequestStatus.Assigned]    = new[] { RequestStatus.InProgress },
        [RequestStatus.InProgress]  = new[] { RequestStatus.Fulfilled },
        [RequestStatus.Fulfilled]   = new[] { RequestStatus.Closed },
    };

    /// Business rule: only valid transitions are allowed (unit-test this!).
    public void ChangeStatus(RequestStatus to)
    {
        if (!Allowed.TryGetValue(Status, out var next) || !next.Contains(to))
            throw new InvalidOperationException($"Cannot move request from {Status} to {to}.");
        Status = to;
        UpdatedAt = DateTime.UtcNow;
    }
}
