using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Api.Extensions;
using Api.Services;
using Application.Strategies;
using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

[ApiController]
[Route("api/requests")]
[Authorize]
public class RequestsController : ControllerBase
{
    private static readonly string[] Categories = { "Food", "Medicine", "Shelter", "Transport", "Accessibility", "Water", "Other" };
    private readonly AppDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly IGeocodingService _geo;
    public RequestsController(AppDbContext db, IUnitOfWork uow, IGeocodingService geo) { _db = db; _uow = uow; _geo = geo; }

    public class SubmitRequestDto
    {
        [Required, StringLength(2000, MinimumLength = 10)] public string Description { get; set; } = "";
        [Required] public string Category { get; set; } = "";
        public int? CrisisId { get; set; }
        [Range(1, 1000)] public int PeopleAffected { get; set; } = 1;
        [Range(-90, 90)] public decimal? Latitude { get; set; }
        [Range(-180, 180)] public decimal? Longitude { get; set; }
        [StringLength(300)] public string? Address { get; set; }
        [Range(1, 5)] public byte? UserUrgency { get; set; }
    }
    public class AssignDto { public int? OrganisationId { get; set; } public int? AssigneeUserId { get; set; } [StringLength(500)] public string? Note { get; set; } }
    public class ChangeStatusDto { [Required] public string Status { get; set; } = ""; [StringLength(1000)] public string? Note { get; set; } }

    private static object ToDto(AssistanceRequest r) => new
    {
        r.RequestId, r.ReferenceNo, r.Category, r.Description, r.PeopleAffected, r.Latitude, r.Longitude, r.FormattedAddress,
        r.UserUrgency, r.FinalPriority, Status = r.Status.ToString(), r.AssignedOrgId, r.AssignedTo, r.DueAt, r.CreatedAt, r.UpdatedAt
    };

    /// Citizen submits a request. The event is saved in the SAME transaction (transactional outbox);
    /// the OutboxProcessor then asks the AI service asynchronously.
    [HttpPost, Authorize(Roles = "Citizen")]
    public async Task<IActionResult> Submit(SubmitRequestDto dto, CancellationToken ct)
    {
        if (!Categories.Contains(dto.Category)) return BadRequest(new { error = $"Category must be one of: {string.Join(", ", Categories)}" });

        var crisis = dto.CrisisId is int cid
            ? await _db.CrisisEvents.FirstOrDefaultAsync(c => c.CrisisId == cid && c.Status == "Active", ct)
            : await _db.CrisisEvents.Where(c => c.Status == "Active").OrderBy(c => c.CrisisId).FirstOrDefaultAsync(ct);
        if (crisis is null) return StatusCode(503, new { error = "No active crisis is configured." });

        var userId = User.GetUserId();
        var lat = dto.Latitude; var lon = dto.Longitude; var formatted = dto.Address;
        if ((lat is null || lon is null) && !string.IsNullOrWhiteSpace(dto.Address))
        {
            var point = await _geo.GeocodeAsync(dto.Address, ct);       // best effort: the request is accepted even if the map service is down
            if (point is not null) { lat = point.Latitude; lon = point.Longitude; formatted = point.FormattedAddress ?? dto.Address; }
        }
        int? householdId = await _db.Households.Where(h => h.UserId == userId).Select(h => (int?)h.HouseholdId).FirstOrDefaultAsync(ct);
        int? areaId = null;
        if (lat is not null && lon is not null)
        {
            var areas = await _db.OperationalAreas.Where(a => a.CrisisId == crisis.CrisisId).ToListAsync(ct);
            areaId = areas.Where(a => GeoMath.HaversineKm((double)lat, (double)lon, (double)a.CentreLat, (double)a.CentreLng) <= (double)a.RadiusKm)
                          .OrderBy(a => GeoMath.HaversineKm((double)lat, (double)lon, (double)a.CentreLat, (double)a.CentreLng)).Select(a => (int?)a.AreaId).FirstOrDefault();
        }
        var req = new AssistanceRequest
        {
            ReferenceNo = $"REQ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            CrisisId = crisis.CrisisId, SubmittedBy = userId, Category = dto.Category, Description = TextSanitizer.Clean(dto.Description, 2000), HouseholdId = householdId, AreaId = areaId,
            PeopleAffected = dto.PeopleAffected, Latitude = lat, Longitude = lon, FormattedAddress = formatted is null ? null : TextSanitizer.Clean(formatted, 400), UserUrgency = dto.UserUrgency
        };

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            _db.AssistanceRequests.Add(req);
            await _uow.SaveChangesAsync(ct);                       // gets RequestId
            _db.RequestHistory.Add(new RequestHistory { RequestId = req.RequestId, ToStatus = "Submitted", ChangedBy = userId, Note = "Request submitted" });
            _db.AuditEvents.Add(new AuditEvent { UserId = userId, Action = "REQUEST_SUBMITTED", EntityType = "AssistanceRequest", EntityId = req.RequestId.ToString() });
            _db.OutboxMessages.Add(new OutboxMessage { EventType = "RequestSubmitted", Payload = JsonSerializer.Serialize(new { RequestId = req.RequestId }) });
            await _uow.SaveChangesAsync(ct);
        }, ct);

        return CreatedAtAction(nameof(Get), new { id = req.RequestId }, new { req.RequestId, req.ReferenceNo, Status = req.Status.ToString() });
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var list = await _db.AssistanceRequests.Where(r => r.SubmittedBy == userId).OrderByDescending(r => r.CreatedAt).Take(100).ToListAsync(ct);
        return Ok(list.Select(ToDto));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var r = await _db.AssistanceRequests.FindAsync(new object[] { id }, ct);
        if (r is null) return NotFound();
        if (r.SubmittedBy != User.GetUserId() && !User.IsStaff()) return Forbid();   // citizens only see their own
        var history = await _db.RequestHistory.Where(h => h.RequestId == id).OrderBy(h => h.ChangedAt)
            .Select(h => new { h.FromStatus, h.ToStatus, h.Note, h.ChangedAt }).ToListAsync(ct);
        return Ok(new { request = ToDto(r), history });
    }

    [HttpGet, Authorize(Roles = "EmergencyCoordinator,CaseManager")]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100); page = Math.Max(page, 1);
        var q = _db.AssistanceRequests.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<RequestStatus>(status, true, out var s)) return BadRequest(new { error = "Unknown status." });
            q = q.Where(r => r.Status == s);
        }
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(r => r.FinalPriority).ThenBy(r => r.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new { total, page, pageSize, items = items.Select(ToDto) });
    }

    [HttpPatch("{id:long}/status"), Authorize(Roles = "EmergencyCoordinator,CaseManager")]
    public async Task<IActionResult> ChangeStatus(long id, ChangeStatusDto dto, CancellationToken ct)
    {
        if (!Enum.TryParse<RequestStatus>(dto.Status, true, out var next)) return BadRequest(new { error = "Unknown status." });
        var r = await _db.AssistanceRequests.FindAsync(new object[] { id }, ct);
        if (r is null) return NotFound();

        var from = r.Status.ToString();
        try { r.ChangeStatus(next); }                                  // business rule lives in the Domain
        catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }

        var uid = User.GetUserId();
        _db.RequestHistory.Add(new RequestHistory { RequestId = id, FromStatus = from, ToStatus = next.ToString(), ChangedBy = uid, Note = dto.Note });
        _db.AuditEvents.Add(new AuditEvent { UserId = uid, Action = "REQUEST_STATUS_CHANGED", EntityType = "AssistanceRequest", EntityId = id.ToString(), OldValue = from, NewValue = next.ToString() });
        Workflow.Notify(_db, id, "STATUS_CHANGED", "Your request was updated", $"Your request {r.ReferenceNo} is now: {next}.");
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(r));
    }

    /// Open / overdue / escalated / unresolved overview for coordinators.
    [HttpGet("monitor"), Authorize(Roles = "EmergencyCoordinator,CaseManager")]
    public async Task<IActionResult> Monitor(CancellationToken ct)
    {
        var done = new[] { RequestStatus.Fulfilled, RequestStatus.Closed, RequestStatus.Rejected, RequestStatus.Merged };
        var now = DateTime.UtcNow;
        var open = _db.AssistanceRequests.Where(r => !done.Contains(r.Status));
        var overdue = await open.Where(r => r.DueAt != null && r.DueAt < now).OrderBy(r => r.DueAt).Take(20)
            .Select(r => new { r.RequestId, r.ReferenceNo, r.Category, r.FinalPriority, r.DueAt, Status = r.Status }).ToListAsync(ct);
        return Ok(new
        {
            open = await open.CountAsync(ct),
            overdue = await open.CountAsync(r => r.DueAt != null && r.DueAt < now, ct),
            escalated = await open.CountAsync(r => r.Status == RequestStatus.Escalated, ct),
            unassigned = await open.CountAsync(r => r.AssignedOrgId == null && r.AssignedTo == null, ct),
            overdueList = overdue
        });
    }

    /// Assign a request to a partner organisation and/or a staff member.
    [HttpPatch("{id:long}/assign"), Authorize(Roles = "EmergencyCoordinator,CaseManager")]
    public async Task<IActionResult> Assign(long id, AssignDto dto, CancellationToken ct)
    {
        var r = await _db.AssistanceRequests.FindAsync(new object[] { id }, ct);
        if (r is null) return NotFound();
        if (dto.OrganisationId is int oid && !await _db.Organisations.AnyAsync(o => o.OrganisationId == oid && o.IsActive, ct)) return BadRequest(new { error = "Unknown or inactive organisation." });
        if (dto.AssigneeUserId is int uid && !await _db.Users.AnyAsync(u => u.UserId == uid && u.IsActive, ct)) return BadRequest(new { error = "Unknown or inactive user." });
        if (dto.OrganisationId is null && dto.AssigneeUserId is null) return BadRequest(new { error = "Provide an organisation and/or a staff member." });

        r.AssignedOrgId = dto.OrganisationId ?? r.AssignedOrgId; r.AssignedTo = dto.AssigneeUserId ?? r.AssignedTo; r.UpdatedAt = DateTime.UtcNow;
        _db.RequestHistory.Add(new RequestHistory { RequestId = id, FromStatus = r.Status.ToString(), ToStatus = r.Status.ToString(), ChangedBy = User.GetUserId(), Note = $"Assigned (org {r.AssignedOrgId}, staff {r.AssignedTo}). {TextSanitizer.Clean(dto.Note, 500)}".Trim() });
        Workflow.Audit(_db, User.GetUserId(), "REQUEST_ASSIGNED", "AssistanceRequest", id.ToString(), null, $"org={r.AssignedOrgId}, user={r.AssignedTo}");
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(r));
    }
}
