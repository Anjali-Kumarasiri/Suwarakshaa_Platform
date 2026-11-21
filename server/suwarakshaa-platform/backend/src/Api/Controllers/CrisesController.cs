using System.ComponentModel.DataAnnotations;
using Api.Extensions;
using Api.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

/// Crisis events, their operational areas and the organisations working on them.
[ApiController]
[Route("api/crises")]
[Authorize]
public class CrisesController : ControllerBase
{
    private static readonly string[] Statuses = { "Planned", "Active", "Contained", "Closed" };
    private readonly AppDbContext _db;
    public CrisesController(AppDbContext db) => _db = db;

    public class CrisisDto
    {
        [Required, StringLength(200, MinimumLength = 3)] public string Name { get; set; } = "";
        [Required, StringLength(40)] public string Type { get; set; } = "";
        [Range(1, 5)] public byte Severity { get; set; } = 3;
        public string Status { get; set; } = "Active";
    }
    public class StatusDto { [Required] public string Status { get; set; } = ""; public bool Force { get; set; } }
    public class AreaDto
    {
        [Required, StringLength(200, MinimumLength = 2)] public string Name { get; set; } = "";
        [Range(-90, 90)] public decimal CentreLat { get; set; }
        [Range(-180, 180)] public decimal CentreLng { get; set; }
        [Range(0.1, 2000)] public decimal RadiusKm { get; set; }
    }
    public class OrgLinkDto { public int OrganisationId { get; set; } [StringLength(100)] public string? RoleInCrisis { get; set; } }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _db.CrisisEvents.OrderByDescending(c => c.StartedAt).Select(c => new { c.CrisisId, c.Name, c.Type, c.Severity, c.Status, c.StartedAt, c.ClosedAt }).ToListAsync(ct));

    [HttpPost, Authorize(Roles = AppRoles.Coordinator)]
    public async Task<IActionResult> Create(CrisisDto dto, CancellationToken ct)
    {
        if (!Statuses.Contains(dto.Status)) return BadRequest(new { error = "Status must be Planned, Active, Contained or Closed." });
        var c = new CrisisEvent { Name = dto.Name.Trim(), Type = dto.Type.Trim(), Severity = dto.Severity, Status = dto.Status, CreatedBy = User.GetUserId() };
        _db.CrisisEvents.Add(c);
        Workflow.Audit(_db, User.GetUserId(), "CRISIS_CREATED", "CrisisEvent", dto.Name);
        await _db.SaveChangesAsync(ct);
        return Created("", new { c.CrisisId });
    }

    /// Closing a crisis with unresolved requests is refused unless Force = true (clear handling of unfinished work).
    [HttpPatch("{id:int}/status"), Authorize(Roles = AppRoles.Coordinator)]
    public async Task<IActionResult> SetStatus(int id, StatusDto dto, CancellationToken ct)
    {
        if (!Statuses.Contains(dto.Status)) return BadRequest(new { error = "Unknown status." });
        var c = await _db.CrisisEvents.FindAsync(new object[] { id }, ct);
        if (c is null) return NotFound();
        if (dto.Status == "Closed")
        {
            var done = new[] { RequestStatus.Fulfilled, RequestStatus.Closed, RequestStatus.Rejected, RequestStatus.Merged };
            var open = await _db.AssistanceRequests.CountAsync(r => r.CrisisId == id && !done.Contains(r.Status), ct);
            if (open > 0 && !dto.Force) return Conflict(new { error = $"{open} request(s) are still unresolved. Resolve them or repeat with force = true.", unresolved = open });
            c.ClosedAt = DateTime.UtcNow;
        }
        var old = c.Status; c.Status = dto.Status;
        Workflow.Audit(_db, User.GetUserId(), "CRISIS_STATUS", "CrisisEvent", id.ToString(), old, dto.Status);
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    // ---- operational areas ----
    [HttpGet("{id:int}/areas")]
    public async Task<IActionResult> Areas(int id, CancellationToken ct) => Ok(await _db.OperationalAreas.Where(a => a.CrisisId == id).ToListAsync(ct));

    [HttpPost("{id:int}/areas"), Authorize(Roles = AppRoles.Staff)]
    public async Task<IActionResult> AddArea(int id, AreaDto dto, CancellationToken ct)
    {
        if (!await _db.CrisisEvents.AnyAsync(c => c.CrisisId == id, ct)) return NotFound();
        var a = new OperationalArea { CrisisId = id, Name = dto.Name.Trim(), CentreLat = dto.CentreLat, CentreLng = dto.CentreLng, RadiusKm = dto.RadiusKm };
        _db.OperationalAreas.Add(a);
        Workflow.Audit(_db, User.GetUserId(), "AREA_CREATED", "OperationalArea", dto.Name);
        await _db.SaveChangesAsync(ct);
        return Created("", new { a.AreaId });
    }

    [HttpDelete("{id:int}/areas/{areaId:int}"), Authorize(Roles = AppRoles.Staff)]
    public async Task<IActionResult> DeleteArea(int id, int areaId, CancellationToken ct)
    {
        var a = await _db.OperationalAreas.FirstOrDefaultAsync(x => x.AreaId == areaId && x.CrisisId == id, ct);
        if (a is null) return NotFound();
        _db.OperationalAreas.Remove(a);
        Workflow.Audit(_db, User.GetUserId(), "AREA_DELETED", "OperationalArea", areaId.ToString());
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    // ---- multi-organisation coordination ----
    [HttpGet("{id:int}/organisations")]
    public async Task<IActionResult> Organisations(int id, CancellationToken ct) =>
        Ok(await (from co in _db.CrisisOrganisations.Where(x => x.CrisisId == id) join o in _db.Organisations on co.OrganisationId equals o.OrganisationId
                  select new { o.OrganisationId, o.Name, o.Type, co.RoleInCrisis }).ToListAsync(ct));

    [HttpPost("{id:int}/organisations"), Authorize(Roles = AppRoles.Coordinator)]
    public async Task<IActionResult> LinkOrganisation(int id, OrgLinkDto dto, CancellationToken ct)
    {
        if (!await _db.CrisisEvents.AnyAsync(c => c.CrisisId == id, ct) || !await _db.Organisations.AnyAsync(o => o.OrganisationId == dto.OrganisationId && o.IsActive, ct)) return NotFound();
        if (await _db.CrisisOrganisations.AnyAsync(x => x.CrisisId == id && x.OrganisationId == dto.OrganisationId, ct)) return Conflict(new { error = "Already linked." });
        _db.CrisisOrganisations.Add(new CrisisOrganisation { CrisisId = id, OrganisationId = dto.OrganisationId, RoleInCrisis = dto.RoleInCrisis });
        Workflow.Audit(_db, User.GetUserId(), "CRISIS_ORG_LINKED", "CrisisEvent", id.ToString(), null, dto.OrganisationId.ToString());
        await _db.SaveChangesAsync(ct);
        return Ok();
    }
}
