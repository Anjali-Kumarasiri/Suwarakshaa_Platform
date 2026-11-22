using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Api.Extensions;
using Api.Services;
using Application.Strategies;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

/// Verified public alerts and emergency announcements (only authorised coordinators can publish).
[ApiController]
[Route("api/alerts")]
public class AlertsController : ControllerBase
{
    private readonly AppDbContext _db;
    public AlertsController(AppDbContext db) => _db = db;

    public class AlertDto
    {
        [Required, StringLength(200, MinimumLength = 3)] public string Title { get; set; } = "";
        [Required, StringLength(2000, MinimumLength = 5)] public string Body { get; set; } = "";
        public string Severity { get; set; } = "Info";
        public string Language { get; set; } = "en";
        public DateTime? ExpiresAt { get; set; }
        public bool NotifyUsers { get; set; }       // true = also send as an emergency announcement (in-app + email)
    }

    private static readonly Dictionary<string, string[]> Guidance = new(StringComparer.OrdinalIgnoreCase)
    {
        ["flood"] = new[] { "Move to higher ground and avoid walking or driving through flood water.", "Switch off electricity at the main switch if water enters your home.", "Keep drinking water, medicines and documents in a waterproof bag.", "Follow instructions from local authorities." },
        ["landslide"] = new[] { "Leave the area if you see cracks, tilting trees or muddy water in streams.", "Move away from the slope path, not along it.", "Do not return until authorities declare it safe." },
        ["earthquake"] = new[] { "Drop, cover and hold on until the shaking stops.", "Stay away from windows and heavy furniture.", "After the shaking, expect aftershocks and check for gas leaks." },
        ["storm"] = new[] { "Stay indoors away from windows.", "Secure loose objects outside.", "Keep a charged phone and torch ready." },
        ["outbreak"] = new[] { "Wash hands often with soap and clean water.", "Drink only safe water.", "Seek medical care early if you have symptoms and avoid crowds." },
    };

    [HttpGet("public"), AllowAnonymous]
    public async Task<IActionResult> Public([FromQuery] string? language, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var q = _db.PublicAlerts.Where(a => a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > now));
        if (!string.IsNullOrWhiteSpace(language)) q = q.Where(a => a.Language == language);
        return Ok(await q.OrderByDescending(a => a.PublishedAt).Take(50).Select(a => new { a.AlertId, a.Title, a.Body, a.Severity, a.Language, a.PublishedAt, a.ExpiresAt }).ToListAsync(ct));
    }

    [HttpGet("guidance"), AllowAnonymous]
    public IActionResult GuidanceFor([FromQuery] string type) =>
        Guidance.TryGetValue(type ?? "", out var tips) ? Ok(new { type, tips }) : NotFound(new { error = "Available: " + string.Join(", ", Guidance.Keys) });

    [HttpPost, Authorize(Roles = AppRoles.Coordinator)]
    public async Task<IActionResult> Publish(AlertDto dto, CancellationToken ct)
    {
        if (!new[] { "Info", "Warning", "Critical" }.Contains(dto.Severity)) return BadRequest(new { error = "Severity must be Info, Warning or Critical." });
        if (!new[] { "en", "si", "ta" }.Contains(dto.Language)) return BadRequest(new { error = "Language must be en, si or ta." });
        var crisis = await _db.CrisisEvents.Where(c => c.Status == "Active").OrderBy(c => c.CrisisId).FirstOrDefaultAsync(ct);
        var a = new PublicAlert
        {
            CrisisId = crisis?.CrisisId, Title = TextSanitizer.Clean(dto.Title, 200), Body = TextSanitizer.Clean(dto.Body, 2000),
            Severity = dto.Severity, Language = dto.Language, PublishedBy = User.GetUserId(), ExpiresAt = dto.ExpiresAt?.ToUniversalTime()
        };
        _db.PublicAlerts.Add(a);
        await _db.SaveChangesAsync(ct);
        Workflow.Audit(_db, User.GetUserId(), "ALERT_PUBLISHED", "PublicAlert", a.AlertId.ToString(), null, a.Severity);
        if (dto.NotifyUsers)
            _db.OutboxMessages.Add(new OutboxMessage { EventType = "AlertPublished", Payload = JsonSerializer.Serialize(new { AlertId = a.AlertId }) });
        await _db.SaveChangesAsync(ct);
        return Created("", new { a.AlertId });
    }

    [HttpPatch("{id:int}/deactivate"), Authorize(Roles = AppRoles.Coordinator)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        var a = await _db.PublicAlerts.FindAsync(new object[] { id }, ct);
        if (a is null) return NotFound();
        a.IsActive = false;
        Workflow.Audit(_db, User.GetUserId(), "ALERT_DEACTIVATED", "PublicAlert", id.ToString());
        await _db.SaveChangesAsync(ct);
        return Ok();
    }
}
