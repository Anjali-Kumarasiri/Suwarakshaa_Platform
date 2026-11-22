using System.ComponentModel.DataAnnotations;
using Api.Extensions;
using Api.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminController(AppDbContext db) => _db = db;

    public class StaffDto
    {
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required, StringLength(100, MinimumLength = 10)] public string Password { get; set; } = "";
        [Required, StringLength(200, MinimumLength = 2)] public string FullName { get; set; } = "";
        [Required] public string Role { get; set; } = "";
    }
    public class RoleDto { [Required] public string Role { get; set; } = ""; }
    public class ActiveDto { public bool IsActive { get; set; } }

    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100); page = Math.Max(page, 1);
        var users = await _db.Users.OrderBy(u => u.UserId).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new { u.UserId, u.Email, u.FullName, u.IsActive, u.CreatedAt }).ToListAsync(ct);
        var ids = users.Select(u => u.UserId).ToList();
        var roles = await (from ur in _db.UserRoles join r in _db.Roles on ur.RoleId equals r.RoleId where ids.Contains(ur.UserId) select new { ur.UserId, r.Name }).ToListAsync(ct);
        return Ok(users.Select(u => new { u.UserId, u.Email, u.FullName, u.IsActive, u.CreatedAt, roles = roles.Where(r => r.UserId == u.UserId).Select(r => r.Name) }));
    }

    [HttpGet("roles")]
    public async Task<IActionResult> Roles(CancellationToken ct) => Ok(await _db.Roles.OrderBy(r => r.Name).Select(r => r.Name).ToListAsync(ct));

    /// Staff accounts are created by an admin (citizens self-register, but cannot give themselves a staff role).
    [HttpPost("users")]
    public async Task<IActionResult> CreateStaff(StaffDto dto, CancellationToken ct)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == dto.Role, ct);
        if (role is null) return BadRequest(new { error = "Unknown role." });
        if (await _db.Users.AnyAsync(u => u.Email == email, ct)) return Conflict(new { error = "Email already in use." });
        var u = new User { Email = email, FullName = dto.FullName.Trim(), PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password) };
        _db.Users.Add(u);
        await _db.SaveChangesAsync(ct);
        _db.UserRoles.Add(new UserRole { UserId = u.UserId, RoleId = role.RoleId });
        Workflow.Audit(_db, User.GetUserId(), "USER_CREATED", "User", u.UserId.ToString(), null, dto.Role);
        await _db.SaveChangesAsync(ct);
        return Created("", new { u.UserId });
    }

    [HttpPost("users/{id:int}/roles")]
    public async Task<IActionResult> AddRole(int id, RoleDto dto, CancellationToken ct)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == dto.Role, ct);
        if (role is null || !await _db.Users.AnyAsync(u => u.UserId == id, ct)) return NotFound();
        if (await _db.UserRoles.AnyAsync(x => x.UserId == id && x.RoleId == role.RoleId, ct)) return Conflict(new { error = "User already has this role." });
        _db.UserRoles.Add(new UserRole { UserId = id, RoleId = role.RoleId });
        Workflow.Audit(_db, User.GetUserId(), "ROLE_ADDED", "User", id.ToString(), null, dto.Role);
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpDelete("users/{id:int}/roles/{roleName}")]
    public async Task<IActionResult> RemoveRole(int id, string roleName, CancellationToken ct)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct);
        if (role is null) return NotFound();
        if (id == User.GetUserId() && roleName == "SystemAdmin") return Conflict(new { error = "You cannot remove your own admin role." });
        var link = await _db.UserRoles.FirstOrDefaultAsync(x => x.UserId == id && x.RoleId == role.RoleId, ct);
        if (link is null) return NotFound();
        _db.UserRoles.Remove(link);
        Workflow.Audit(_db, User.GetUserId(), "ROLE_REMOVED", "User", id.ToString(), roleName, null);
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpPost("users/{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, ActiveDto dto, CancellationToken ct)
    {
        if (id == User.GetUserId() && !dto.IsActive) return Conflict(new { error = "You cannot deactivate yourself." });
        var u = await _db.Users.FindAsync(new object[] { id }, ct);
        if (u is null) return NotFound();
        u.IsActive = dto.IsActive;
        Workflow.Audit(_db, User.GetUserId(), dto.IsActive ? "USER_ACTIVATED" : "USER_DEACTIVATED", "User", id.ToString());
        await _db.SaveChangesAsync(ct);
        return Ok();
    }
}
