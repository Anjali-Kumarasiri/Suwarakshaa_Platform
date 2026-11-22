using System.ComponentModel.DataAnnotations;
using Api.Extensions;
using Api.Services;
using Application.Abstractions;
using Application.Strategies;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

[ApiController]
[Route("api/shelters")]
public class SheltersController : ControllerBase
{
    private readonly AppDbContext _db; private readonly IUnitOfWork _uow;
    public SheltersController(AppDbContext db, IUnitOfWork uow) { _db = db; _uow = uow; }

    public class ShelterDto
    {
        [Required, StringLength(200, MinimumLength = 2)] public string Name { get; set; } = "";
        [StringLength(400)] public string? Address { get; set; }
        [Range(-90, 90)] public decimal Latitude { get; set; }
        [Range(-180, 180)] public decimal Longitude { get; set; }
        [Range(1, 100000)] public int Capacity { get; set; }
        public bool IsAccessible { get; set; }
        public bool IsPublic { get; set; }
        public int OrganisationId { get; set; } = 1;
    }
    public class FacilityDto { [Required, StringLength(60, MinimumLength = 2)] public string Facility { get; set; } = ""; }
    public class ServiceAreaDto { [Range(0.5, 200)] public decimal RadiusKm { get; set; } [StringLength(300)] public string? Notes { get; set; } }
    public class ResourceRequestDto { public int ResourceTypeId { get; set; } [Range(0.01, 1000000)] public decimal Quantity { get; set; } public int PartnerOrganisationId { get; set; } }
    public class AllocateDto { public long RequestId { get; set; } [Range(1, 10000)] public int PeopleCount { get; set; } = 1; public long? RecommendationId { get; set; } }

    /// Public view for the citizen portal: only shelters marked public and not closed. No internal data.
    [HttpGet("public"), AllowAnonymous]
    public async Task<IActionResult> Public(CancellationToken ct) =>
        Ok(await _db.Shelters.Where(s => s.IsPublic && s.Status != "Closed")
            .Select(s => new { s.ShelterId, s.Name, s.Address, s.Latitude, s.Longitude, s.IsAccessible, s.Status, Spaces = s.Capacity - s.CurrentOccupancy })
            .ToListAsync(ct));

    [HttpGet, Authorize(Roles = AppRoles.ShelterOps)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _db.Shelters.OrderBy(s => s.Name).Select(s => new
        {
            s.ShelterId, s.Name, s.Address, s.Latitude, s.Longitude, s.Capacity, s.CurrentOccupancy, s.IsAccessible, s.IsPublic, s.Status,
            OccupancyPct = s.Capacity == 0 ? 0 : Math.Round(100.0 * s.CurrentOccupancy / s.Capacity, 1)
        }).ToListAsync(ct));

    [HttpPost, Authorize(Roles = AppRoles.ShelterOps)]
    public async Task<IActionResult> Create(ShelterDto dto, CancellationToken ct)
    {
        var crisis = await _db.CrisisEvents.Where(c => c.Status == "Active").OrderBy(c => c.CrisisId).FirstOrDefaultAsync(ct);
        if (crisis is null) return StatusCode(503, new { error = "No active crisis." });
        var s = new Shelter
        {
            CrisisId = crisis.CrisisId, OrganisationId = dto.OrganisationId, AdminUserId = User.GetUserId(), Name = dto.Name.Trim(),
            Address = dto.Address, Latitude = dto.Latitude, Longitude = dto.Longitude, Capacity = dto.Capacity,
            IsAccessible = dto.IsAccessible, IsPublic = dto.IsPublic
        };
        _db.Shelters.Add(s);
        Workflow.Audit(_db, User.GetUserId(), "SHELTER_CREATED", "Shelter", dto.Name);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(List), new { id = s.ShelterId }, new { s.ShelterId });
    }

    /// Ranked shelter suggestions for a request (capacity = hard rule, accessibility, distance, spare room).
    [HttpGet("suggest"), Authorize(Roles = AppRoles.ShelterOps)]
    public async Task<IActionResult> Suggest([FromQuery] long requestId, CancellationToken ct)
    {
        var req = await _db.AssistanceRequests.FindAsync(new object[] { requestId }, ct);
        if (req is null) return NotFound();
        var shelters = await _db.Shelters.Where(s => s.Status == "Open" && s.CrisisId == req.CrisisId).ToListAsync(ct);
        var needAccessible = req.Category == "Accessibility";
        var cands = shelters.Select(s => new ShelterCandidate(s.ShelterId, s.Name,
            req.Latitude is null || req.Longitude is null ? 9999 : GeoMath.HaversineKm((double)req.Latitude, (double)req.Longitude, (double)s.Latitude, (double)s.Longitude),
            s.Capacity - s.CurrentOccupancy, s.IsAccessible));
        return Ok(ShelterRanker.Rank(cands, req.PeopleAffected, needAccessible));
    }

    /// Allocation can never exceed capacity: the capacity check and the update are ONE atomic SQL statement,
    /// so two coordinators clicking at the same time cannot over-allocate. (DB CHECK constraint is a second safety net.)
    [HttpPost("{id:int}/allocations"), Authorize(Roles = AppRoles.ShelterOps)]
    public async Task<IActionResult> Allocate(int id, AllocateDto dto, CancellationToken ct)
    {
        var req = await _db.AssistanceRequests.FindAsync(new object[] { dto.RequestId }, ct);
        var shelter = await _db.Shelters.FindAsync(new object[] { id }, ct);
        if (req is null || shelter is null) return NotFound();
        if (await _db.ShelterAllocations.AnyAsync(a => a.RequestId == dto.RequestId && (a.Status == "Allocated" || a.Status == "CheckedIn"), ct))
            return Conflict(new { error = "This request already has an active shelter allocation." });

        var n = dto.PeopleCount; var userId = User.GetUserId(); bool ok = false;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            var rows = await _db.Shelters.Where(s => s.ShelterId == id && s.Status != "Closed" && s.CurrentOccupancy + n <= s.Capacity)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.CurrentOccupancy, s => s.CurrentOccupancy + n), ct);
            if (rows == 0) return;
            await _db.Shelters.Where(s => s.ShelterId == id && s.CurrentOccupancy >= s.Capacity)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, "Full"), ct);

            _db.ShelterAllocations.Add(new ShelterAllocation { ShelterId = id, RequestId = dto.RequestId, PeopleCount = n, RecommendationId = dto.RecommendationId, AllocatedBy = userId });
            Workflow.MarkAssigned(_db, req, userId, $"Shelter allocated: {shelter.Name}");
            Workflow.Audit(_db, userId, "SHELTER_ALLOCATED", "AssistanceRequest", req.RequestId.ToString(), null, $"{shelter.Name} x{n}");
            Workflow.Notify(_db, req.RequestId, "SHELTER_ALLOCATED", "A shelter has been assigned", $"You have been assigned to {shelter.Name}{(shelter.Address is null ? "" : ", " + shelter.Address)}.");
            await _db.SaveChangesAsync(ct);
            ok = true;
        }, ct);

        return ok ? Ok(new { message = "Allocated." })
                  : Conflict(new { error = "Not enough spare capacity (or shelter closed). Try /api/shelters/suggest for alternatives." });
    }

    [HttpPost("allocations/{allocationId:long}/checkout"), Authorize(Roles = AppRoles.ShelterOps)]
    public async Task<IActionResult> CheckOut(long allocationId, CancellationToken ct)
    {
        var a = await _db.ShelterAllocations.FindAsync(new object[] { allocationId }, ct);
        if (a is null) return NotFound();
        if (a.Status is "CheckedOut" or "Cancelled") return Conflict(new { error = "Already released." });
        var n = a.PeopleCount; var sid = a.ShelterId;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            a.Status = "CheckedOut"; a.CheckedOutAt = DateTime.UtcNow;
            await _db.Shelters.Where(s => s.ShelterId == sid)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.CurrentOccupancy, s => s.CurrentOccupancy - n >= 0 ? s.CurrentOccupancy - n : 0), ct);
            await _db.Shelters.Where(s => s.ShelterId == sid && s.Status == "Full" && s.CurrentOccupancy < s.Capacity)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, "Open"), ct);
            Workflow.Audit(_db, User.GetUserId(), "SHELTER_CHECKOUT", "ShelterAllocation", allocationId.ToString());
            await _db.SaveChangesAsync(ct);
        }, ct);
        return Ok();
    }

    [HttpGet("{id:int}/facilities"), AllowAnonymous]
    public async Task<IActionResult> Facilities(int id, CancellationToken ct) =>
        Ok(await _db.ShelterFacilities.Where(f => f.ShelterId == id).Select(f => f.Facility).ToListAsync(ct));

    [HttpPost("{id:int}/facilities"), Authorize(Roles = AppRoles.ShelterOps)]
    public async Task<IActionResult> AddFacility(int id, FacilityDto dto, CancellationToken ct)
    {
        if (!await _db.Shelters.AnyAsync(s => s.ShelterId == id, ct)) return NotFound();
        var name = dto.Facility.Trim();
        if (await _db.ShelterFacilities.AnyAsync(f => f.ShelterId == id && f.Facility == name, ct)) return Conflict(new { error = "Facility already listed." });
        _db.ShelterFacilities.Add(new ShelterFacility { ShelterId = id, Facility = name });
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpDelete("{id:int}/facilities/{facility}"), Authorize(Roles = AppRoles.ShelterOps)]
    public async Task<IActionResult> RemoveFacility(int id, string facility, CancellationToken ct)
    {
        var f = await _db.ShelterFacilities.FirstOrDefaultAsync(x => x.ShelterId == id && x.Facility == facility, ct);
        if (f is null) return NotFound();
        _db.ShelterFacilities.Remove(f);
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    /// Service radius used by the coverage analysis (GET api/analytics/coverage).
    [HttpPut("{id:int}/service-area"), Authorize(Roles = AppRoles.ShelterOps)]
    public async Task<IActionResult> SetServiceArea(int id, ServiceAreaDto dto, CancellationToken ct)
    {
        if (!await _db.Shelters.AnyAsync(s => s.ShelterId == id, ct)) return NotFound();
        var sa = await _db.ServiceAreas.FirstOrDefaultAsync(x => x.ShelterId == id, ct);
        if (sa is null) { sa = new ServiceArea { ShelterId = id }; _db.ServiceAreas.Add(sa); }
        sa.RadiusKm = dto.RadiusKm; sa.Notes = dto.Notes;
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    /// A shelter asks a partner organisation for more supplies when it runs short.
    [HttpPost("{id:int}/resource-requests"), Authorize(Roles = AppRoles.ShelterOps)]
    public async Task<IActionResult> RequestResources(int id, ResourceRequestDto dto, CancellationToken ct)
    {
        var s = await _db.Shelters.FindAsync(new object[] { id }, ct);
        if (s is null) return NotFound();
        if (!await _db.ResourceTypes.AnyAsync(t => t.ResourceTypeId == dto.ResourceTypeId, ct) || !await _db.Organisations.AnyAsync(o => o.OrganisationId == dto.PartnerOrganisationId && o.IsActive, ct))
            return BadRequest(new { error = "Unknown resource type or partner organisation." });
        _db.PartnerAssistanceRequests.Add(new PartnerAssistanceRequest { FromOrganisationId = s.OrganisationId, ToOrganisationId = dto.PartnerOrganisationId, ResourceTypeId = dto.ResourceTypeId, Quantity = dto.Quantity });
        Workflow.Audit(_db, User.GetUserId(), "SHELTER_RESOURCE_REQUEST", "Shelter", id.ToString(), null, $"type {dto.ResourceTypeId} x{dto.Quantity}");
        await _db.SaveChangesAsync(ct);
        return Created("", new { message = "Request sent to the partner organisation." });
    }
}
