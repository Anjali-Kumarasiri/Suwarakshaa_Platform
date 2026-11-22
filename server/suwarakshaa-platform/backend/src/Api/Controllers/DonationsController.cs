using System.ComponentModel.DataAnnotations;
using Api.Extensions;
using Api.Services;
using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Api.Controllers;

[ApiController]
[Route("api/donations")]
public class DonationsController : ControllerBase
{
    private const decimal LargeAmount = 500_000m;
    private readonly AppDbContext _db; private readonly IUnitOfWork _uow;
    public DonationsController(AppDbContext db, IUnitOfWork uow) { _db = db; _uow = uow; }

    public class CampaignDto
    {
        [Required, StringLength(200, MinimumLength = 3)] public string Name { get; set; } = "";
        [Range(1, 1_000_000_000)] public decimal TargetAmount { get; set; }
        public DateOnly? EndsOn { get; set; }
        public int OrganisationId { get; set; } = 1;
    }
    public class DonateDto
    {
        public int CampaignId { get; set; }
        [Required] public string Type { get; set; } = "Monetary";
        [Range(0.01, 10_000_000)] public decimal? Amount { get; set; }
        [StringLength(3, MinimumLength = 3)] public string? Currency { get; set; }
        public int? ResourceTypeId { get; set; }
        [Range(0.01, 10_000_000)] public decimal? Quantity { get; set; }
        [StringLength(100)] public string? PaymentRef { get; set; }     // client-supplied idempotency key
        public bool Anonymous { get; set; }
    }
    public class DonorDto { [Required, StringLength(200, MinimumLength = 2)] public string Name { get; set; } = ""; public string Type { get; set; } = "Individual"; [EmailAddress] public string? Email { get; set; } public bool IsAnonymous { get; set; } }
    public class SpendDto { [Required, StringLength(200, MinimumLength = 3)] public string Purpose { get; set; } = ""; [Range(0.01, 1_000_000_000)] public decimal Amount { get; set; } }
    public class ReviewDto { public bool Clear { get; set; } }

    // ---------- public ----------
    [HttpGet("campaigns"), AllowAnonymous]
    public async Task<IActionResult> Campaigns(CancellationToken ct)
    {
        var camps = await _db.Campaigns.Where(c => c.Status == "Active").ToListAsync(ct);
        var raised = await _db.Donations.Where(d => d.Type == "Monetary" && !d.IsFlagged).GroupBy(d => d.CampaignId).Select(g => new { g.Key, Sum = g.Sum(x => x.Amount ?? 0) }).ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
        return Ok(camps.Select(c => new
        {
            c.CampaignId, c.Name, c.TargetAmount, c.EndsOn,
            Raised = raised.GetValueOrDefault(c.CampaignId), Pct = Math.Round(100 * raised.GetValueOrDefault(c.CampaignId) / c.TargetAmount, 1)
        }));
    }

    /// Transparency: how much was raised and what it was spent on. Never exposes donor identities.
    [HttpGet("transparency/{campaignId:int}"), AllowAnonymous]
    public async Task<IActionResult> Transparency(int campaignId, CancellationToken ct)
    {
        var c = await _db.Campaigns.FindAsync(new object[] { campaignId }, ct);
        if (c is null) return NotFound();
        var raised = await _db.Donations.Where(d => d.CampaignId == campaignId && d.Type == "Monetary" && !d.IsFlagged).SumAsync(d => d.Amount ?? 0, ct);
        var spends = await (from a in _db.DonationAllocations join d in _db.Donations on a.DonationId equals d.DonationId
                            where d.CampaignId == campaignId select new { a.Purpose, a.AmountUsed, a.AllocatedAt }).ToListAsync(ct);
        var spent = spends.Sum(s => s.AmountUsed ?? 0);
        return Ok(new { c.Name, raised, spent, remaining = raised - spent, spending = spends.OrderByDescending(s => s.AllocatedAt) });
    }

    // ---------- donors ----------
    [HttpPost, Authorize]
    public async Task<IActionResult> Donate(DonateDto dto, CancellationToken ct)
    {
        var campaign = await _db.Campaigns.FindAsync(new object[] { dto.CampaignId }, ct);
        if (campaign is null || campaign.Status != "Active") return NotFound(new { error = "Campaign not found or closed." });

        if (dto.Type == "Monetary") { if (dto.Amount is null || string.IsNullOrWhiteSpace(dto.Currency)) return BadRequest(new { error = "Amount and currency are required." }); }
        else if (dto.Type == "Material") { if (dto.ResourceTypeId is null || dto.Quantity is null) return BadRequest(new { error = "Resource type and quantity are required." }); }
        else return BadRequest(new { error = "Type must be Monetary or Material." });

        // idempotency: the same payment reference never creates two donations
        var payRef = string.IsNullOrWhiteSpace(dto.PaymentRef) ? "SBX-" + Guid.NewGuid().ToString("N")[..12] : dto.PaymentRef.Trim();
        var existing = await _db.Donations.FirstOrDefaultAsync(d => d.PaymentRef == payRef, ct);
        if (existing is not null) return Ok(new { existing.DonationId, existing.PaymentRef, duplicate = true });

        var uid = User.GetUserId();
        var donor = await _db.Donors.FirstOrDefaultAsync(d => d.UserId == uid, ct);
        if (donor is null)
        {
            var u = await _db.Users.FindAsync(new object[] { uid }, ct);
            donor = new Donor { UserId = uid, Name = u!.FullName, Email = u.Email };
            _db.Donors.Add(donor);
            await _db.SaveChangesAsync(ct);
        }
        donor.IsAnonymous = dto.Anonymous;

        // simple fraud / anomaly rules -> flagged for human review (never auto-rejected)
        string? flag = null;
        if (dto.Type == "Monetary" && dto.Amount >= LargeAmount) flag = "Unusually large amount";
        var recent = await _db.Donations.CountAsync(d => d.DonorId == donor.DonorId && d.ReceivedAt > DateTime.UtcNow.AddMinutes(-10), ct);
        if (recent >= 5) flag = (flag is null ? "" : flag + "; ") + "High donation frequency";

        var don = new Donation
        {
            DonorId = donor.DonorId, CampaignId = dto.CampaignId, Type = dto.Type, Amount = dto.Amount, Currency = dto.Currency?.ToUpperInvariant(),
            ResourceTypeId = dto.ResourceTypeId, Quantity = dto.Quantity, PaymentRef = payRef, IsFlagged = flag is not null, FlagReason = flag
        };
        _db.Donations.Add(don);
        Workflow.Audit(_db, uid, flag is null ? "DONATION_RECEIVED" : "DONATION_FLAGGED", "Donation", payRef, null, flag);
        // in-app notification so the donor sees a pop-up / bell entry in the portal
        _db.Notifications.Add(new Notification
        {
            UserId = uid, Channel = "InApp", TemplateCode = "DONATION_RECEIVED", Language = "en",
            Subject = "Thank you for your donation",
            Body = dto.Type == "Monetary"
                ? $"We received your donation of {dto.Currency?.ToUpperInvariant()} {dto.Amount:N0} to '{campaign.Name}'. Reference {payRef}."
                : $"We received your material donation to '{campaign.Name}'. Reference {payRef}.",
            IdempotencyKey = "DON:" + payRef, Status = "Sent", SentAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return Created("", new { don.DonationId, don.PaymentRef, flagged = don.IsFlagged });
    }

    [HttpGet("mine"), Authorize]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var uid = User.GetUserId();
        return Ok(await (from d in _db.Donations join dn in _db.Donors on d.DonorId equals dn.DonorId
                         where dn.UserId == uid orderby d.ReceivedAt descending
                         select new { d.DonationId, d.CampaignId, d.Type, d.Amount, d.Currency, d.Quantity, d.ReceivedAt, d.IsFlagged }).Take(100).ToListAsync(ct));
    }

    // ---------- donor officers ----------
    [HttpPost("campaigns"), Authorize(Roles = AppRoles.DonorOps)]
    public async Task<IActionResult> CreateCampaign(CampaignDto dto, CancellationToken ct)
    {
        var crisis = await _db.CrisisEvents.Where(c => c.Status == "Active").OrderBy(c => c.CrisisId).FirstOrDefaultAsync(ct);
        if (crisis is null) return StatusCode(503, new { error = "No active crisis." });
        var c = new Campaign { CrisisId = crisis.CrisisId, OrganisationId = dto.OrganisationId, Name = dto.Name.Trim(), TargetAmount = dto.TargetAmount, StartsOn = DateOnly.FromDateTime(DateTime.UtcNow), EndsOn = dto.EndsOn };
        _db.Campaigns.Add(c);
        Workflow.Audit(_db, User.GetUserId(), "CAMPAIGN_CREATED", "Campaign", dto.Name);
        await _db.SaveChangesAsync(ct);
        return Created("", new { c.CampaignId });
    }

    [HttpGet("flagged"), Authorize(Roles = AppRoles.DonorOps)]
    public async Task<IActionResult> Flagged(CancellationToken ct) =>
        Ok(await _db.Donations.Where(d => d.IsFlagged).OrderByDescending(d => d.ReceivedAt).Take(100).ToListAsync(ct));

    [HttpPost("{id:long}/review"), Authorize(Roles = AppRoles.DonorOps)]
    public async Task<IActionResult> Review(long id, ReviewDto dto, CancellationToken ct)
    {
        var d = await _db.Donations.FindAsync(new object[] { id }, ct);
        if (d is null) return NotFound();
        if (dto.Clear) { d.IsFlagged = false; }
        Workflow.Audit(_db, User.GetUserId(), dto.Clear ? "DONATION_CLEARED" : "DONATION_KEPT_FLAGGED", "Donation", id.ToString());
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    /// Record spending against a campaign. Spending is split over that campaign's cleared donations (oldest first)
    /// and can never exceed what was actually raised.
    [HttpPost("campaigns/{campaignId:int}/spend"), Authorize(Roles = AppRoles.DonorOps)]
    public async Task<IActionResult> Spend(int campaignId, SpendDto dto, CancellationToken ct)
    {
        var uid = User.GetUserId(); bool ok = false, flagged = false;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            var donations = await _db.Donations.Where(d => d.CampaignId == campaignId && d.Type == "Monetary" && !d.IsFlagged).OrderBy(d => d.ReceivedAt).ToListAsync(ct);
            var used = await _db.DonationAllocations.GroupBy(a => a.DonationId).Select(g => new { g.Key, Sum = g.Sum(x => x.AmountUsed ?? 0) }).ToDictionaryAsync(x => x.Key, x => x.Sum, ct);
            var left = dto.Amount;
            foreach (var d in donations)
            {
                var free = (d.Amount ?? 0) - used.GetValueOrDefault(d.DonationId);
                if (free <= 0) continue;
                var take = Math.Min(free, left);
                _db.DonationAllocations.Add(new DonationAllocation { DonationId = d.DonationId, Purpose = dto.Purpose.Trim(), AmountUsed = take, ApprovedBy = uid });
                left -= take;
                if (left == 0) break;
            }
            if (left > 0) return;                      // not enough funds: nothing is saved
            var raisedTotal = donations.Sum(d => d.Amount ?? 0);
            flagged = raisedTotal > 0 && dto.Amount > raisedTotal * 0.5m;          // large single allocation -> flag for human review
            Workflow.Audit(_db, uid, flagged ? "ALLOCATION_FLAGGED" : "DONATION_SPENT", "Campaign", campaignId.ToString(), null, $"{dto.Amount} for {dto.Purpose}");
            await _db.SaveChangesAsync(ct);
            ok = true;
        }, ct);
        return ok ? Ok(new { flaggedForReview = flagged }) : Conflict(new { error = "Not enough cleared funds in this campaign." });
    }

    // ---------- donor registration ----------
    [HttpPut("donor-profile"), Authorize]
    public async Task<IActionResult> SaveDonorProfile(DonorDto dto, CancellationToken ct)
    {
        if (!new[] { "Individual", "Corporate", "Foundation", "Government" }.Contains(dto.Type)) return BadRequest(new { error = "Unknown donor type." });
        var uid = User.GetUserId();
        var d = await _db.Donors.FirstOrDefaultAsync(x => x.UserId == uid, ct);
        if (d is null) { d = new Donor { UserId = uid }; _db.Donors.Add(d); }
        d.Name = dto.Name.Trim(); d.Type = dto.Type; d.Email = dto.Email; d.IsAnonymous = dto.IsAnonymous;
        await _db.SaveChangesAsync(ct);
        return Ok(new { d.DonorId });
    }

    [HttpGet("donor-profile"), Authorize]
    public async Task<IActionResult> GetDonorProfile(CancellationToken ct)
    {
        var uid = User.GetUserId();
        var d = await _db.Donors.Where(x => x.UserId == uid).Select(x => new { x.DonorId, x.Name, x.Type, x.Email, x.IsAnonymous }).FirstOrDefaultAsync(ct);
        return d is null ? NotFound() : Ok(d);
    }

    /// Large allocations that were flagged for review (from the audit trail).
    [HttpGet("flagged-allocations"), Authorize(Roles = AppRoles.DonorOps)]
    public async Task<IActionResult> FlaggedAllocations(CancellationToken ct) =>
        Ok(await _db.AuditEvents.Where(a => a.Action == "ALLOCATION_FLAGGED").OrderByDescending(a => a.OccurredAt).Take(100).ToListAsync(ct));
}
