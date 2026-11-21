using System.Linq.Expressions;
using Application.Abstractions;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // identity & crisis
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Organisation> Organisations => Set<Organisation>();
    public DbSet<CrisisEvent> CrisisEvents => Set<CrisisEvent>();
    // requests & AI governance
    public DbSet<AssistanceRequest> AssistanceRequests => Set<AssistanceRequest>();
    public DbSet<RequestHistory> RequestHistory => Set<RequestHistory>();
    public DbSet<DuplicateCandidate> DuplicateCandidates => Set<DuplicateCandidate>();
    public DbSet<AiRecommendation> AiRecommendations => Set<AiRecommendation>();
    public DbSet<HumanDecision> HumanDecisions => Set<HumanDecision>();
    // shelters & resources
    public DbSet<Shelter> Shelters => Set<Shelter>();
    public DbSet<ShelterAllocation> ShelterAllocations => Set<ShelterAllocation>();
    public DbSet<ResourceType> ResourceTypes => Set<ResourceType>();
    public DbSet<ResourceInventory> ResourceInventories => Set<ResourceInventory>();
    public DbSet<ResourceAllocation> ResourceAllocations => Set<ResourceAllocation>();
    public DbSet<PartnerAssistanceRequest> PartnerAssistanceRequests => Set<PartnerAssistanceRequest>();
    // volunteers
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Volunteer> Volunteers => Set<Volunteer>();
    public DbSet<VolunteerSkill> VolunteerSkills => Set<VolunteerSkill>();
    public DbSet<VolunteerAvailability> VolunteerAvailability => Set<VolunteerAvailability>();
    public DbSet<VolunteerTask> VolunteerTasks => Set<VolunteerTask>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    // donations
    public DbSet<Donor> Donors => Set<Donor>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<DonationAllocation> DonationAllocations => Set<DonationAllocation>();
    // messaging, audit, map
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationAttempt> NotificationAttempts => Set<NotificationAttempt>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<GeocodeCache> GeocodeCache => Set<GeocodeCache>();
    // profile, evidence, feedback, areas, dispatch, analytics, alerts
    public DbSet<Household> Households => Set<Household>();
    public DbSet<RequestAttachment> RequestAttachments => Set<RequestAttachment>();
    public DbSet<Feedback> Feedback => Set<Feedback>();
    public DbSet<OperationalArea> OperationalAreas => Set<OperationalArea>();
    public DbSet<CrisisOrganisation> CrisisOrganisations => Set<CrisisOrganisation>();
    public DbSet<ShelterFacility> ShelterFacilities => Set<ShelterFacility>();
    public DbSet<ServiceArea> ServiceAreas => Set<ServiceArea>();
    public DbSet<DispatchRecord> DispatchRecords => Set<DispatchRecord>();
    public DbSet<KpiSnapshot> KpiSnapshots => Set<KpiSnapshot>();
    public DbSet<PublicAlert> PublicAlerts => Set<PublicAlert>();

    protected override void ConfigureConventions(ModelConfigurationBuilder cb)
        => cb.Properties<decimal>().HavePrecision(14, 2);

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(x => x.Email).IsUnique();
        b.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
        b.Entity<CrisisEvent>().HasKey(x => x.CrisisId);

        b.Entity<AssistanceRequest>(e =>
        {
            e.HasKey(x => x.RequestId);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);   // DB column is NVARCHAR
            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
        });
        b.Entity<RequestHistory>().HasKey(x => x.HistoryId);
        b.Entity<DuplicateCandidate>(e => { e.HasKey(x => x.DuplicateId); e.Property(x => x.SimilarityScore).HasPrecision(5, 4); });
        b.Entity<AiRecommendation>(e =>
        {
            e.HasKey(x => x.RecommendationId);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Confidence).HasPrecision(5, 4);
        });
        b.Entity<HumanDecision>(e => { e.HasKey(x => x.DecisionId); e.Property(x => x.Action).HasConversion<string>().HasMaxLength(10); });
        b.Entity<AuditEvent>().HasKey(x => x.AuditId);
        b.Entity<OutboxMessage>().HasKey(x => x.MessageId);

        b.Entity<Shelter>(e => { e.Property(x => x.Latitude).HasPrecision(9, 6); e.Property(x => x.Longitude).HasPrecision(9, 6); });
        b.Entity<ShelterAllocation>().HasKey(x => x.AllocationId);
        b.Entity<ResourceInventory>(e =>
        {
            e.HasKey(x => x.InventoryId);
            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
        });
        b.Entity<PartnerAssistanceRequest>().HasKey(x => x.PartnerRequestId);

        b.Entity<Volunteer>(e => { e.Property(x => x.BaseLatitude).HasPrecision(9, 6); e.Property(x => x.BaseLongitude).HasPrecision(9, 6); });
        b.Entity<VolunteerSkill>().HasKey(x => new { x.VolunteerId, x.SkillId });
        b.Entity<VolunteerAvailability>().HasKey(x => x.AvailabilityId);
        b.Entity<VolunteerTask>().HasKey(x => x.TaskId);
        b.Entity<TaskAssignment>(e => { e.HasKey(x => x.AssignmentId); e.Property(x => x.HoursLogged).HasPrecision(5, 2); });

        b.Entity<NotificationAttempt>().HasKey(x => x.AttemptId);
        b.Entity<RequestAttachment>().HasKey(x => x.AttachmentId);
        b.Entity<OperationalArea>(e => { e.HasKey(x => x.AreaId); e.Property(x => x.CentreLat).HasPrecision(9, 6); e.Property(x => x.CentreLng).HasPrecision(9, 6); e.Property(x => x.RadiusKm).HasPrecision(8, 2); });
        b.Entity<CrisisOrganisation>().HasKey(x => new { x.CrisisId, x.OrganisationId });
        b.Entity<ShelterFacility>().HasKey(x => new { x.ShelterId, x.Facility });
        b.Entity<ServiceArea>().Property(x => x.RadiusKm).HasPrecision(8, 2);
        b.Entity<DispatchRecord>(e => { e.HasKey(x => x.DispatchId); e.Property(x => x.DistanceKm).HasPrecision(8, 2); });
        b.Entity<KpiSnapshot>(e =>
        {
            e.HasKey(x => x.SnapshotId);
            e.Property(x => x.AvgResponseMinutes).HasPrecision(10, 2);
            e.Property(x => x.ResourceUtilisationPct).HasPrecision(5, 2);
            e.Property(x => x.ShelterOccupancyPct).HasPrecision(5, 2);
            e.Property(x => x.AiOverrideRatePct).HasPrecision(5, 2);
        });
        b.Entity<PublicAlert>().HasKey(x => x.AlertId);
        b.Entity<GeocodeCache>(e => { e.HasKey(x => x.CacheId); e.Property(x => x.Latitude).HasPrecision(9, 6); e.Property(x => x.Longitude).HasPrecision(9, 6); });
    }
}

public class Repository<T> : IRepository<T> where T : class
{
    private readonly AppDbContext _db;
    public Repository(AppDbContext db) => _db = db;
    public async Task<T?> GetByIdAsync(long id, CancellationToken ct = default) => await _db.Set<T>().FindAsync(new object[] { id }, ct);
    public async Task AddAsync(T entity, CancellationToken ct = default) => await _db.Set<T>().AddAsync(entity, ct);
    public IQueryable<T> Query() => _db.Set<T>();
    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => _db.Set<T>().AnyAsync(predicate, ct);
    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => _db.Set<T>().FirstOrDefaultAsync(predicate, ct);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    public UnitOfWork(AppDbContext db) => _db = db;
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    public async Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try { await work(); await tx.CommitAsync(ct); }
        catch { await tx.RollbackAsync(ct); throw; }
    }
}
