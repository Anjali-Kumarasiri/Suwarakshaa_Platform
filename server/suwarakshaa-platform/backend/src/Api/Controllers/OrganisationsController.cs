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
[Route("api/organisations")]
[Authorize]
public class OrganisationsController : ControllerBase
{
    private static readonly string[] Types = { "Government", "NGO", "International", "Partner" };
    private readonly AppDbContext _db;
    public OrganisationsController(AppDbContext db) => _db = db;

    public class OrgDto
    {
        [Required, StringLength(200, MinimumLength = 2)] public string Name { get; set; } = "";
        [Required] public string Type { get; set; } = "";
        [StringLength(100)] public string? Country { get; set; }
        [EmailAddress, StringLength(256)] public string? ContactEmail { get; set; }
    }
    public class ActiveDto { public bool IsActive { get; set; } }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _db.Organisations.OrderBy(o => o.Name).Select(o => new { o.OrganisationId, o.Name, o.Type, o.Country, o.IsActive }).ToListAsync(ct));

    [HttpPost, Authorize(Roles = "EmergencyCoordinator,SystemAdmin")]
    public async Task<IActionResult> Create(OrgDto dto, CancellationToken ct)
    {
        if (!Types.Contains(dto.Type)) return BadRequest(new { error = $"Type must be one of {string.Join(", ", Types)}." });
        if (await _db.Organisations.AnyAsync(o => o.Name == dto.Name.Trim(), ct)) return Conflict(new { error = "An organisation with this name exists." });
        var o = new Organisation { Name = dto.Name.Trim(), Type = dto.Type, Country = dto.Country, ContactEmail = dto.ContactEmail };
        _db.Organisations.Add(o);
        Workflow.Audit(_db, User.GetUserId(), "ORG_CREATED", "Organisation", dto.Name);
        await _db.SaveChangesAsync(ct);
        return Created("", new { o.OrganisationId });
    }

    [HttpPatch("{id:int}/active"), Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> SetActive(int id, ActiveDto dto, CancellationToken ct)
    {
        var o = await _db.Organisations.FindAsync(new object[] { id }, ct);
        if (o is null) return NotFound();
        o.IsActive = dto.IsActive;
        Workflow.Audit(_db, User.GetUserId(), dto.IsActive ? "ORG_ACTIVATED" : "ORG_DEACTIVATED", "Organisation", id.ToString());
        await _db.SaveChangesAsync(ct);
        return Ok();
    }
}
