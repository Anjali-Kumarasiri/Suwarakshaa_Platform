namespace Domain.Entities;

public class Household
{
    public int HouseholdId { get; set; }
    public int UserId { get; set; }
    public int MemberCount { get; set; } = 1;
    public bool HasChildren { get; set; }
    public bool HasElderly { get; set; }
    public bool HasDisability { get; set; }
    public byte[]? MedicalNotesEnc { get; set; }   // encrypted (ASP.NET Data Protection)
    public byte[]? AddressEnc { get; set; }        // encrypted
}

public class RequestAttachment
{
    public long AttachmentId { get; set; }
    public long RequestId { get; set; }
    public string StoredFileName { get; set; } = default!;     // server-generated, never the client's name
    public string OriginalFileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = default!;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class Feedback
{
    public long FeedbackId { get; set; }
    public long RequestId { get; set; }
    public byte Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OperationalArea
{
    public int AreaId { get; set; }
    public int CrisisId { get; set; }
    public string Name { get; set; } = default!;
    public decimal CentreLat { get; set; }
    public decimal CentreLng { get; set; }
    public decimal RadiusKm { get; set; }
    public string? BoundaryGeoJson { get; set; }
}

public class CrisisOrganisation { public int CrisisId { get; set; } public int OrganisationId { get; set; } public string? RoleInCrisis { get; set; } }
public class ShelterFacility { public int ShelterId { get; set; } public string Facility { get; set; } = default!; }
public class ServiceArea { public int ServiceAreaId { get; set; } public int ShelterId { get; set; } public decimal RadiusKm { get; set; } public string? Notes { get; set; } }

public class DispatchRecord
{
    public long DispatchId { get; set; }
    public long ResourceAllocationId { get; set; }
    public int? VehicleInventoryId { get; set; }
    public int? DriverUserId { get; set; }
    public decimal? DistanceKm { get; set; }
    public int? EtaMinutes { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string Status { get; set; } = "Pending";   // Pending | InTransit | Delivered | Failed
}

public class KpiSnapshot
{
    public long SnapshotId { get; set; }
    public int? CrisisId { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public int UnresolvedCases { get; set; }
    public decimal? AvgResponseMinutes { get; set; }
    public decimal? ResourceUtilisationPct { get; set; }
    public decimal? ShelterOccupancyPct { get; set; }
    public decimal? AiOverrideRatePct { get; set; }
}

public class PublicAlert
{
    public int AlertId { get; set; }
    public int? CrisisId { get; set; }
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string Severity { get; set; } = "Info";    // Info | Warning | Critical
    public string Language { get; set; } = "en";
    public int PublishedBy { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}
