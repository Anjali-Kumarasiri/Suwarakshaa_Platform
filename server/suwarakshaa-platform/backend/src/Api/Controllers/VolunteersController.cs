using System.ComponentModel.DataAnnotations;
using Api.Extensions;
using Api.Services;
using Application.Strategies;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

[ApiController]
[Route("api/volunteers")]
[Authorize]
public class VolunteersController : ControllerBase
{
    private readonly AppDbContext _db;
    public VolunteersController(AppDbContext db) => _db = db;

    public class RegisterDto
    {
        [Range(-90, 90)] public decimal? BaseLatitude { get; set; }
        [Range(-180, 180)] public decimal? BaseLongitude { get; set; }
        public bool HasVehicle { get; set; }
        public List<SkillLevel> Skills { get; set; } = new();
    }
    public class SkillLevel { public int SkillId { get; set; } [Range(1, 5)] public byte Level { get; set; } = 1; }
    public class VerifyDto { [Required] public string Status { get; set; } = ""; }
    public class AvailabilityDto { public DateTime StartsAt { get; set; } public DateTime EndsAt { get; set; } }
    public class TaskDto
    {
        [Required, StringLength(200, MinimumLength = 3)] public string Title { get; set; } = "";
        [StringLength(1000)] public string? Description { get; set; }
        public int? RequiredSkillId { get; set; }
        [Range(1, 500)] public int VolunteersNeeded { get; set; } = 1;
        public DateTime? StartsAt { get; set; }
        public long? RequestId { get; set; }
        public int? ShelterId { get; set; }
    }
    public class AssignDto { public int VolunteerId { get; set; } }
    public class RespondDto { public bool Accept { get; set; } }
    public class CompleteDto { [Range(0.25, 24)] public decimal Hours { get; set; } }

    [HttpGet("skills")]
    public async Task<IActionResult> Skills(CancellationToken ct) => Ok(await _db.Skills.OrderBy(s => s.Name).ToListAsync(ct));

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto, CancellationToken ct)
    {
        var uid = User.GetUserId();
        if (await _db.Volunteers.AnyAsync(v => v.UserId == uid, ct)) return Conflict(new { error = "You are already registered as a volunteer." });
        var skillIds = dto.Skills.Select(s => s.SkillId).Distinct().ToList();
        if (await _db.Skills.CountAsync(s => skillIds.Contains(s.SkillId), ct) != skillIds.Count) return BadRequest(new { error = "Unknown skill id." });

        var v = new Volunteer { UserId = uid, BaseLatitude = dto.BaseLatitude, BaseLongitude = dto.BaseLongitude, HasVehicle = dto.HasVehicle };
        _db.Volunteers.Add(v);
        await _db.SaveChangesAsync(ct);
        _db.VolunteerSkills.AddRange(dto.Skills.GroupBy(s => s.SkillId).Select(g => new VolunteerSkill { VolunteerId = v.VolunteerId, SkillId = g.Key, Level = g.First().Level }));
        Workflow.Audit(_db, uid, "VOLUNTEER_REGISTERED", "Volunteer", v.VolunteerId.ToString());
        await _db.SaveChangesAsync(ct);
        return Created("", new { v.VolunteerId, status = v.VerificationStatus });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var uid = User.GetUserId();
        var v = await _db.Volunteers.FirstOrDefaultAsync(x => x.UserId == uid, ct);
        if (v is null) return NotFound();
        var skills = await (from vs in _db.VolunteerSkills.Where(x => x.VolunteerId == v.VolunteerId) join s in _db.Skills on vs.SkillId equals s.SkillId select new { s.Name, vs.Level }).ToListAsync(ct);
        return Ok(new { v.VolunteerId, v.VerificationStatus, v.HasVehicle, skills });
    }

    [HttpPost("availability")]
    public async Task<IActionResult> AddAvailability(AvailabilityDto dto, CancellationToken ct)
    {
        if (dto.EndsAt <= dto.StartsAt) return BadRequest(new { error = "End must be after start." });
        var uid = User.GetUserId();
        var v = await _db.Volunteers.FirstOrDefaultAsync(x => x.UserId == uid, ct);
        if (v is null) return NotFound(new { error = "Register as a volunteer first." });
        _db.VolunteerAvailability.Add(new VolunteerAvailability { VolunteerId = v.VolunteerId, StartsAt = dto.StartsAt.ToUniversalTime(), EndsAt = dto.EndsAt.ToUniversalTime() });
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    // ---------- coordinator side ----------
    [HttpGet, Authorize(Roles = AppRoles.VolunteerOps)]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
    {
        var q = _db.Volunteers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(v => v.VerificationStatus == status);
        return Ok(await (from v in q join u in _db.Users on v.UserId equals u.UserId
                         select new { v.VolunteerId, u.FullName, u.Email, v.VerificationStatus, v.HasVehicle }).Take(200).ToListAsync(ct));
    }

    [HttpPost("{id:int}/verification"), Authorize(Roles = AppRoles.VolunteerOps)]
    public async Task<IActionResult> Verify(int id, VerifyDto dto, CancellationToken ct)
    {
        if (!new[] { "Verified", "Rejected", "Suspended" }.Contains(dto.Status)) return BadRequest(new { error = "Status must be Verified, Rejected or Suspended." });
        var v = await _db.Volunteers.FindAsync(new object[] { id }, ct);
        if (v is null) return NotFound();
        var old = v.VerificationStatus; v.VerificationStatus = dto.Status; v.VerifiedBy = User.GetUserId();
        Workflow.Audit(_db, User.GetUserId(), "VOLUNTEER_" + dto.Status.ToUpperInvariant(), "Volunteer", id.ToString(), old, dto.Status);
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpPost("tasks"), Authorize(Roles = AppRoles.VolunteerOps)]
    public async Task<IActionResult> CreateTask(TaskDto dto, CancellationToken ct)
    {
        var crisis = await _db.CrisisEvents.Where(c => c.Status == "Active").OrderBy(c => c.CrisisId).FirstOrDefaultAsync(ct);
        if (crisis is null) return StatusCode(503, new { error = "No active crisis." });
        var t = new VolunteerTask
        {
            CrisisId = crisis.CrisisId, Title = dto.Title.Trim(), Description = dto.Description, RequiredSkillId = dto.RequiredSkillId,
            VolunteersNeeded = dto.VolunteersNeeded, StartsAt = dto.StartsAt?.ToUniversalTime(), RequestId = dto.RequestId, ShelterId = dto.ShelterId, CreatedBy = User.GetUserId()
        };
        _db.VolunteerTasks.Add(t);
        Workflow.Audit(_db, User.GetUserId(), "TASK_CREATED", "VolunteerTask", dto.Title);
        await _db.SaveChangesAsync(ct);
        return Created("", new { t.TaskId });
    }

    [HttpGet("tasks")]
    public async Task<IActionResult> Tasks([FromQuery] string? status = "Open", CancellationToken ct = default)
    {
        var q = _db.VolunteerTasks.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(t => t.Status == status);
        return Ok(await q.OrderBy(t => t.StartsAt).Take(100).ToListAsync(ct));
    }

    /// Ranked volunteer matches: verified only, required skill, availability window, not already assigned, distance, vehicle.
    [HttpGet("tasks/{id:int}/matches"), Authorize(Roles = AppRoles.VolunteerOps)]
    public async Task<IActionResult> Matches(int id, CancellationToken ct)
    {
        var task = await _db.VolunteerTasks.FindAsync(new object[] { id }, ct);
        if (task is null) return NotFound();
        decimal? lat = null, lon = null;
        if (task.ShelterId is int sid) { var s = await _db.Shelters.FindAsync(new object[] { sid }, ct); lat = s?.Latitude; lon = s?.Longitude; }
        else if (task.RequestId is long rid) { var r = await _db.AssistanceRequests.FindAsync(new object[] { rid }, ct); lat = r?.Latitude; lon = r?.Longitude; }

        var assigned = await _db.TaskAssignments.Where(a => a.TaskId == id && a.Status != "Declined").Select(a => a.VolunteerId).ToListAsync(ct);
        var vols = await _db.Volunteers.Where(v => v.VerificationStatus == "Verified" && !assigned.Contains(v.VolunteerId)).ToListAsync(ct);
        var skills = await _db.VolunteerSkills.ToListAsync(ct);
        var windows = await _db.VolunteerAvailability.ToListAsync(ct);

        var ranked = vols.Select(v =>
        {
            var skill = task.RequiredSkillId is null ? null : skills.FirstOrDefault(s => s.VolunteerId == v.VolunteerId && s.SkillId == task.RequiredSkillId);
            bool hasSkill = task.RequiredSkillId is null || skill is not null;
            var mine = windows.Where(w => w.VolunteerId == v.VolunteerId).ToList();
            bool available = task.StartsAt is null || mine.Count == 0 || mine.Any(w => w.StartsAt <= task.StartsAt && w.EndsAt >= task.StartsAt);
            double km = lat is null || v.BaseLatitude is null ? 9999 : GeoMath.HaversineKm((double)lat, (double)lon!, (double)v.BaseLatitude, (double)v.BaseLongitude!);
            return new { v.VolunteerId, hasSkill, level = (int?)skill?.Level ?? 0, available, distanceKm = Math.Round(km, 1), v.HasVehicle };
        })
        .Where(x => x.hasSkill && x.available)
        .OrderByDescending(x => x.level).ThenBy(x => x.distanceKm).ThenByDescending(x => x.HasVehicle).Take(20);
        return Ok(ranked);
    }

    [HttpPost("tasks/{id:int}/assign"), Authorize(Roles = AppRoles.VolunteerOps)]
    public async Task<IActionResult> Assign(int id, AssignDto dto, CancellationToken ct)
    {
        var task = await _db.VolunteerTasks.FindAsync(new object[] { id }, ct);
        var vol = await _db.Volunteers.FindAsync(new object[] { dto.VolunteerId }, ct);
        if (task is null || vol is null) return NotFound();
        if (vol.VerificationStatus != "Verified") return Conflict(new { error = "Only verified volunteers can be assigned." });
        if (task.Status is "Filled" or "Completed" or "Cancelled") return Conflict(new { error = $"Task is {task.Status}." });
        if (await _db.TaskAssignments.AnyAsync(a => a.TaskId == id && a.VolunteerId == dto.VolunteerId, ct)) return Conflict(new { error = "Already assigned." });

        _db.TaskAssignments.Add(new TaskAssignment { TaskId = id, VolunteerId = dto.VolunteerId });
        Workflow.Audit(_db, User.GetUserId(), "TASK_ASSIGNED", "VolunteerTask", id.ToString(), null, dto.VolunteerId.ToString());
        await _db.SaveChangesAsync(ct);
        return Ok(new { status = "Offered" });
    }

    [HttpGet("assignments/mine")]
    public async Task<IActionResult> MyAssignments(CancellationToken ct)
    {
        var uid = User.GetUserId();
        return Ok(await (from a in _db.TaskAssignments
                         join v in _db.Volunteers on a.VolunteerId equals v.VolunteerId
                         join t in _db.VolunteerTasks on a.TaskId equals t.TaskId
                         where v.UserId == uid orderby t.StartsAt
                         select new { a.AssignmentId, t.Title, t.Description, t.StartsAt, a.Status, a.HoursLogged }).ToListAsync(ct));
    }

    [HttpPost("assignments/{id:int}/respond")]
    public async Task<IActionResult> Respond(int id, RespondDto dto, CancellationToken ct)
    {
        var uid = User.GetUserId();
        var a = await _db.TaskAssignments.FindAsync(new object[] { id }, ct);
        if (a is null) return NotFound();
        var owner = await _db.Volunteers.Where(v => v.VolunteerId == a.VolunteerId).Select(v => v.UserId).FirstAsync(ct);
        if (owner != uid) return Forbid();
        if (a.Status != "Offered") return Conflict(new { error = $"Assignment is already {a.Status}." });

        a.Status = dto.Accept ? "Accepted" : "Declined"; a.RespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        var task = await _db.VolunteerTasks.FindAsync(new object[] { a.TaskId }, ct);
        if (task is not null && dto.Accept && await _db.TaskAssignments.CountAsync(x => x.TaskId == a.TaskId && x.Status == "Accepted", ct) >= task.VolunteersNeeded)
        { task.Status = "Filled"; await _db.SaveChangesAsync(ct); }
        return Ok(new { a.Status });
    }

    [HttpPost("assignments/{id:int}/complete"), Authorize(Roles = AppRoles.VolunteerOps)]
    public async Task<IActionResult> Complete(int id, CompleteDto dto, CancellationToken ct)
    {
        var a = await _db.TaskAssignments.FindAsync(new object[] { id }, ct);
        if (a is null) return NotFound();
        if (a.Status != "Accepted") return Conflict(new { error = "Only accepted assignments can be completed." });
        a.Status = "Completed"; a.HoursLogged = dto.Hours; a.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        if (!await _db.TaskAssignments.AnyAsync(x => x.TaskId == a.TaskId && x.Status == "Accepted", ct))
        {
            var t = await _db.VolunteerTasks.FindAsync(new object[] { a.TaskId }, ct);
            if (t is not null) { t.Status = "Completed"; await _db.SaveChangesAsync(ct); }
        }
        return Ok();
    }
}
