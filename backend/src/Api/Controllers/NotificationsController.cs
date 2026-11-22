using Api.Extensions;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;
    public NotificationsController(AppDbContext db) => _db = db;

    /// In-app notifications for the logged-in user (the "inbox" in the citizen portal).
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var uid = User.GetUserId();
        return Ok(await _db.Notifications.Where(n => n.UserId == uid && n.Channel == "InApp").OrderByDescending(n => n.CreatedAt).Take(50)
            .Select(n => new { n.NotificationId, n.RequestId, n.Subject, n.Body, n.CreatedAt }).ToListAsync(ct));
    }

    /// Monitoring: notifications that failed permanently (dead letters).
    [HttpGet("dead-letters"), Authorize(Roles = "SystemAdmin,EmergencyCoordinator")]
    public async Task<IActionResult> DeadLetters(CancellationToken ct) =>
        Ok(await _db.Notifications.Where(n => n.Status == "DeadLetter").OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync(ct));
}
