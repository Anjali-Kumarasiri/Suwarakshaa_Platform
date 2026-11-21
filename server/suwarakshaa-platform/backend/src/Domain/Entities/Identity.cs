namespace Domain.Entities;

public class User
{
    public int UserId { get; set; }
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Phone { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Role
{
    public int RoleId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
}

public class UserRole
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
}

public class CrisisEvent
{
    public int CrisisId { get; set; }
    public string Name { get; set; } = default!;
    public string Type { get; set; } = default!;
    public byte Severity { get; set; } = 3;
    public string Status { get; set; } = "Active";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public int CreatedBy { get; set; }
}

public class RequestHistory
{
    public long HistoryId { get; set; }
    public long RequestId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = default!;
    public int? ChangedBy { get; set; }
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
