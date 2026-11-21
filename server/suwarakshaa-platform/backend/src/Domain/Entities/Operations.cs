namespace Domain.Entities;

public class Organisation
{
    public int OrganisationId { get; set; }
    public string Name { get; set; } = default!;
    public string Type { get; set; } = "NGO";
    public string? Country { get; set; }
    public string? ContactEmail { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Shelter
{
    public int ShelterId { get; set; }
    public int CrisisId { get; set; }
    public int OrganisationId { get; set; }
    public int? AdminUserId { get; set; }
    public string Name { get; set; } = default!;
    public string Type { get; set; } = "Shelter";
    public string? Address { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int Capacity { get; set; }
    public int CurrentOccupancy { get; set; }
    public bool IsAccessible { get; set; }
    public bool IsPublic { get; set; }
    public string Status { get; set; } = "Open";     // Open | Full | Closed
}

public class ShelterAllocation
{
    public long AllocationId { get; set; }
    public int ShelterId { get; set; }
    public long RequestId { get; set; }
    public int PeopleCount { get; set; }
    public string Status { get; set; } = "Allocated"; // Allocated | CheckedIn | CheckedOut | Cancelled
    public long? RecommendationId { get; set; }
    public int AllocatedBy { get; set; }
    public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CheckedOutAt { get; set; }
}

public class ResourceType
{
    public int ResourceTypeId { get; set; }
    public string Name { get; set; } = default!;
    public string Category { get; set; } = "Other";
    public string Unit { get; set; } = default!;
    public bool IsPerishable { get; set; }
}

public class ResourceInventory
{
    public int InventoryId { get; set; }
    public int ResourceTypeId { get; set; }
    public int OrganisationId { get; set; }
    public int? CrisisId { get; set; }
    public string? LocationName { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal QuantityTotal { get; set; }
    public decimal QuantityAllocated { get; set; }
    public decimal ReorderLevel { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}

public class ResourceAllocation
{
    public long ResourceAllocationId { get; set; }
    public int InventoryId { get; set; }
    public long RequestId { get; set; }
    public decimal Quantity { get; set; }
    public string Status { get; set; } = "Reserved";  // Reserved | Dispatched | Delivered | Cancelled | Preempted
    public long? RecommendationId { get; set; }
    public Guid? ConflictGroupId { get; set; }
    public int AllocatedBy { get; set; }
    public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;
}

public class PartnerAssistanceRequest
{
    public long PartnerRequestId { get; set; }
    public int FromOrganisationId { get; set; }
    public int ToOrganisationId { get; set; }
    public int ResourceTypeId { get; set; }
    public decimal Quantity { get; set; }
    public string Status { get; set; } = "Pending";   // Pending | Accepted | Declined | Fulfilled
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DuplicateCandidate
{
    public long DuplicateId { get; set; }
    public long RequestId { get; set; }
    public long OtherRequestId { get; set; }
    public decimal SimilarityScore { get; set; }
    public string Status { get; set; } = "Pending";   // Pending | ConfirmedDuplicate | NotDuplicate | Merged
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class GeocodeCache
{
    public long CacheId { get; set; }
    public string QueryHash { get; set; } = default!;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? FormattedAddress { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}
