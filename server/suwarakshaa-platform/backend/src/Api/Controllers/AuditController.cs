using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Roles = "SystemAdmin,EmergencyCoordinator")]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _db;
    public AuditController(AppDbContext db) => _db = db;

    /// Read-only: there is intentionally no endpoint that edits or deletes audit events.
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? entityType, [FromQuery] string? entityId, [FromQuery] int? userId,
                                          [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200); page = Math.Max(page, 1);
        var q = _db.AuditEvents.AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(entityId)) q = q.Where(a => a.EntityId == entityId);
        if (userId is not null) q = q.Where(a => a.UserId == userId);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.OccurredAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Ok(new { total, page, pageSize, items });
    }
}
