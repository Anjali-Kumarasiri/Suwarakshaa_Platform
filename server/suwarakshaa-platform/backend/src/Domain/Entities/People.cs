namespace Domain.Entities;

public class Skill { public int SkillId { get; set; } public string Name { get; set; } = default!; }

public class Volunteer
{
    public int VolunteerId { get; set; }
    public int UserId { get; set; }
    public int? OrganisationId { get; set; }
    public string VerificationStatus { get; set; } = "Pending";   // Pending | Verified | Rejected | Suspended
    public int? VerifiedBy { get; set; }
    public decimal? BaseLatitude { get; set; }
    public decimal? BaseLongitude { get; set; }
    public bool HasVehicle { get; set; }
}
public class VolunteerSkill { public int VolunteerId { get; set; } public int SkillId { get; set; } public byte Level { get; set; } = 1; }
public class VolunteerAvailability
{
    public int AvailabilityId { get; set; }
    public int VolunteerId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
}
public class VolunteerTask
{
    public int TaskId { get; set; }
    public int CrisisId { get; set; }
    public long? RequestId { get; set; }
    public int? ShelterId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public int? RequiredSkillId { get; set; }
    public int VolunteersNeeded { get; set; } = 1;
    public DateTime? StartsAt { get; set; }
    public string Status { get; set; } = "Open";     // Open | Filled | InProgress | Completed | Cancelled
    public int CreatedBy { get; set; }
}
public class TaskAssignment
{
    public int AssignmentId { get; set; }
    public int TaskId { get; set; }
    public int VolunteerId { get; set; }
    public long? RecommendationId { get; set; }
    public string Status { get; set; } = "Offered";  // Offered | Accepted | Declined | Completed | NoShow
    public decimal? HoursLogged { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class Donor
{
    public int DonorId { get; set; }
    public int? UserId { get; set; }
    public string Name { get; set; } = default!;
    public string Type { get; set; } = "Individual";
    public string? Email { get; set; }
    public bool IsAnonymous { get; set; }
}
public class Campaign
{
    public int CampaignId { get; set; }
    public int CrisisId { get; set; }
    public int OrganisationId { get; set; }
    public string Name { get; set; } = default!;
    public decimal TargetAmount { get; set; }
    public DateOnly StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public string Status { get; set; } = "Active";
}
public class Donation
{
    public long DonationId { get; set; }
    public int DonorId { get; set; }
    public int CampaignId { get; set; }
    public string Type { get; set; } = "Monetary";   // Monetary | Material
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public int? ResourceTypeId { get; set; }
    public decimal? Quantity { get; set; }
    public string? PaymentRef { get; set; }
    public bool IsFlagged { get; set; }
    public string? FlagReason { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
public class DonationAllocation
{
    public long DonationAllocationId { get; set; }
    public long DonationId { get; set; }
    public string Purpose { get; set; } = default!;
    public decimal? AmountUsed { get; set; }
    public int? InventoryId { get; set; }
    public int ApprovedBy { get; set; }
    public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public long NotificationId { get; set; }
    public int UserId { get; set; }
    public long? RequestId { get; set; }
    public string Channel { get; set; } = "InApp";      // Email | Sms | Push | InApp
    public string TemplateCode { get; set; } = default!;
    public string Language { get; set; } = "en";
    public string? Subject { get; set; }
    public string Body { get; set; } = default!;
    public string IdempotencyKey { get; set; } = default!;
    public string Status { get; set; } = "Pending";     // Pending | Sent | Failed | DeadLetter
    public int RetryCount { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}
public class NotificationAttempt
{
    public long AttemptId { get; set; }
    public long NotificationId { get; set; }
    public string Provider { get; set; } = default!;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
}
