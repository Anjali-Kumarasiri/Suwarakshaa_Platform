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

/// Household / individual profile. Medical notes and address are ENCRYPTED in the database.
[ApiController]
[Route("api/households")]
[Authorize]
public class HouseholdsController : ControllerBase
{
    private readonly AppDbContext _db; private readonly SensitiveDataProtector _prot;
    public HouseholdsController(AppDbContext db, SensitiveDataProtector prot) { _db = db; _prot = prot; }

    public class HouseholdDto
    {
        [Range(1, 50)] public int MemberCount { get; set; } = 1;
        public bool HasChildren { get; set; }
        public bool HasElderly { get; set; }
        public bool HasDisability { get; set; }
        [StringLength(1000)] public string? MedicalNotes { get; set; }
        [StringLength(300)] public string? Address { get; set; }
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var uid = User.GetUserId();
        var h = await _db.Households.FirstOrDefaultAsync(x => x.UserId == uid, ct);
        if (h is null) return NotFound();
        return Ok(new { h.MemberCount, h.HasChildren, h.HasElderly, h.HasDisability, MedicalNotes = _prot.Unprotect(h.MedicalNotesEnc), Address = _prot.Unprotect(h.AddressEnc) });
    }

    [HttpPut("me")]
    public async Task<IActionResult> Upsert(HouseholdDto dto, CancellationToken ct)
    {
        var uid = User.GetUserId();
        var h = await _db.Households.FirstOrDefaultAsync(x => x.UserId == uid, ct);
        if (h is null) { h = new Household { UserId = uid }; _db.Households.Add(h); }
        h.MemberCount = dto.MemberCount; h.HasChildren = dto.HasChildren; h.HasElderly = dto.HasElderly; h.HasDisability = dto.HasDisability;
        h.MedicalNotesEnc = _prot.Protect(TextSanitizer.Clean(dto.MedicalNotes, 1000));
        h.AddressEnc = _prot.Protect(TextSanitizer.Clean(dto.Address, 300));
        Workflow.Audit(_db, uid, "HOUSEHOLD_SAVED", "Household", uid.ToString());
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    /// Staff may read the vulnerability flags of the household behind a request. Every read of sensitive data is audited.
    [HttpGet("for-request/{requestId:long}"), Authorize(Roles = "EmergencyCoordinator,CaseManager")]
    public async Task<IActionResult> ForRequest(long requestId, CancellationToken ct)
    {
        var req = await _db.AssistanceRequests.FindAsync(new object[] { requestId }, ct);
        if (req is null || req.HouseholdId is null) return NotFound();
        var h = await _db.Households.FindAsync(new object[] { req.HouseholdId.Value }, ct);
        if (h is null) return NotFound();
        Workflow.Audit(_db, User.GetUserId(), "SENSITIVE_DATA_VIEWED", "Household", h.HouseholdId.ToString());
        await _db.SaveChangesAsync(ct);
        return Ok(new { h.MemberCount, h.HasChildren, h.HasElderly, h.HasDisability, MedicalNotes = _prot.Unprotect(h.MedicalNotesEnc) });
    }
}
