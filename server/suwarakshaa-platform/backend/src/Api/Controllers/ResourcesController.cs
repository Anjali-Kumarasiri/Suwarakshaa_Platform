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
[Route("api/resources")]
[Authorize(Roles = AppRoles.LogisticsOps)]
public class ResourcesController : ControllerBase
{
    private readonly AppDbContext _db; private readonly IUnitOfWork _uow; private readonly IAllocationStrategy _strategy; private readonly IRoutingService _routing;
    public ResourcesController(AppDbContext db, IUnitOfWork uow, IAllocationStrategy strategy, IRoutingService routing) { _db = db; _uow = uow; _strategy = strategy; _routing = routing; }

    public class InventoryDto
    {
        [Range(1, int.MaxValue)] public int ResourceTypeId { get; set; }
        public int OrganisationId { get; set; } = 1;
        [StringLength(200)] public string? LocationName { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        [Range(0, 10000000)] public decimal Quantity { get; set; }
        [Range(0, 10000000)] public decimal ReorderLevel { get; set; }
        public DateOnly? ExpiryDate { get; set; }
    }
    public class QuantityDto { [Range(0.01, 10000000)] public decimal Quantity { get; set; } }
    public class AllocateDto
    {
        [Range(1, int.MaxValue)] public int InventoryId { get; set; }
        public long RequestId { get; set; }
        [Range(0.01, 10000000)] public decimal Quantity { get; set; }
        public long? RecommendationId { get; set; }
    }
    public class ShortageDto
    {
        [Range(1, int.MaxValue)] public int InventoryId { get; set; }
        [Required, MinLength(1)] public List<ShortageLine> Requests { get; set; } = new();
        public int? PartnerOrganisationId { get; set; }
    }
    public class ShortageLine { public long RequestId { get; set; } [Range(0.01, 10000000)] public decimal Quantity { get; set; } }
    public class DispatchDto { public int? VehicleInventoryId { get; set; } public int? DriverUserId { get; set; } }
    public class StatusDto { [Required] public string Status { get; set; } = ""; }

    [HttpGet("types")]
    public async Task<IActionResult> Types(CancellationToken ct) => Ok(await _db.ResourceTypes.OrderBy(t => t.Name).ToListAsync(ct));

    [HttpGet("inventory")]
    public async Task<IActionResult> Inventory([FromQuery] int? resourceTypeId, CancellationToken ct)
    {
        var q = _db.ResourceInventories.AsQueryable();
        if (resourceTypeId is not null) q = q.Where(i => i.ResourceTypeId == resourceTypeId);
        var list = await (from i in q join t in _db.ResourceTypes on i.ResourceTypeId equals t.ResourceTypeId
                          orderby t.Name
                          select new
                          {
                              i.InventoryId, Resource = t.Name, t.Unit, i.LocationName, i.QuantityTotal, i.QuantityAllocated,
                              Available = i.QuantityTotal - i.QuantityAllocated, i.ReorderLevel, i.ExpiryDate,
                              LowStock = (i.QuantityTotal - i.QuantityAllocated) <= i.ReorderLevel
                          }).ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost("inventory")]
    public async Task<IActionResult> AddInventory(InventoryDto dto, CancellationToken ct)
    {
        if (!await _db.ResourceTypes.AnyAsync(t => t.ResourceTypeId == dto.ResourceTypeId, ct)) return BadRequest(new { error = "Unknown resource type." });
        var crisis = await _db.CrisisEvents.Where(c => c.Status == "Active").OrderBy(c => c.CrisisId).FirstOrDefaultAsync(ct);
        var inv = new ResourceInventory
        {
            ResourceTypeId = dto.ResourceTypeId, OrganisationId = dto.OrganisationId, CrisisId = crisis?.CrisisId, LocationName = dto.LocationName,
            Latitude = dto.Latitude, Longitude = dto.Longitude, QuantityTotal = dto.Quantity, ReorderLevel = dto.ReorderLevel, ExpiryDate = dto.ExpiryDate
        };
        _db.ResourceInventories.Add(inv);
        Workflow.Audit(_db, User.GetUserId(), "INVENTORY_ADDED", "ResourceInventory", dto.LocationName ?? "-", null, dto.Quantity.ToString());
        await _db.SaveChangesAsync(ct);
        return Created("", new { inv.InventoryId });
    }

    [HttpPost("inventory/{id:int}/restock")]
    public async Task<IActionResult> Restock(int id, QuantityDto dto, CancellationToken ct)
    {
        var q = dto.Quantity;
        var rows = await _db.ResourceInventories.Where(i => i.InventoryId == id)
            .ExecuteUpdateAsync(u => u.SetProperty(i => i.QuantityTotal, i => i.QuantityTotal + q), ct);
        if (rows == 0) return NotFound();
        Workflow.Audit(_db, User.GetUserId(), "INVENTORY_RESTOCK", "ResourceInventory", id.ToString(), null, q.ToString());
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    /// Ranked inventories that can serve a request (not just "nearest": see MultiConstraintAllocationStrategy).
    [HttpGet("suggest")]
    public async Task<IActionResult> Suggest([FromQuery] long requestId, [FromQuery] int resourceTypeId, [FromQuery] decimal quantity, CancellationToken ct)
    {
        var req = await _db.AssistanceRequests.FindAsync(new object[] { requestId }, ct);
        if (req is null) return NotFound();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invs = await _db.ResourceInventories.Where(i => i.ResourceTypeId == resourceTypeId && (i.ExpiryDate == null || i.ExpiryDate >= today)).ToListAsync(ct);
        var cands = invs.Select(i => new Candidate(i.InventoryId, i.QuantityTotal - i.QuantityAllocated,
            req.Latitude is null || i.Latitude is null ? 9999 : GeoMath.HaversineKm((double)req.Latitude, (double)req.Longitude!, (double)i.Latitude, (double)i.Longitude!),
            i.ExpiryDate is not null && i.ExpiryDate <= today.AddDays(14), i.OrganisationId));
        return Ok(_strategy.Rank(cands, new AllocationNeed(quantity, req.FinalPriority ?? 3, 1)));
    }

    /// Atomic reservation: "enough stock?" and "reserve it" are one SQL statement, so stock can never go negative
    /// even if two coordinators reserve the same item at the same moment.
    private async Task<bool> TryReserve(int inventoryId, decimal q, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _db.ResourceInventories
            .Where(i => i.InventoryId == inventoryId && i.QuantityAllocated + q <= i.QuantityTotal && (i.ExpiryDate == null || i.ExpiryDate >= today))
            .ExecuteUpdateAsync(u => u.SetProperty(i => i.QuantityAllocated, i => i.QuantityAllocated + q), ct) == 1;
    }

    [HttpPost("allocate")]
    public async Task<IActionResult> Allocate(AllocateDto dto, CancellationToken ct)
    {
        var req = await _db.AssistanceRequests.FindAsync(new object[] { dto.RequestId }, ct);
        if (req is null) return NotFound(new { error = "Request not found." });
        var userId = User.GetUserId(); bool ok = false;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            if (!await TryReserve(dto.InventoryId, dto.Quantity, ct)) return;
            _db.ResourceAllocations.Add(new ResourceAllocation { InventoryId = dto.InventoryId, RequestId = dto.RequestId, Quantity = dto.Quantity, RecommendationId = dto.RecommendationId, AllocatedBy = userId });
            Workflow.MarkAssigned(_db, req, userId, "Resources allocated");
            Workflow.Audit(_db, userId, "RESOURCE_ALLOCATED", "AssistanceRequest", req.RequestId.ToString(), null, $"inventory {dto.InventoryId} x{dto.Quantity}");
            Workflow.Notify(_db, req.RequestId, "RESOURCE_ALLOCATED", "Help is on the way", "Supplies have been reserved for your request.");
            await _db.SaveChangesAsync(ct);
            ok = true;
        }, ct);
        if (ok) return Ok(new { message = "Reserved." });

        var available = await _db.ResourceInventories.Where(i => i.InventoryId == dto.InventoryId).Select(i => i.QuantityTotal - i.QuantityAllocated).FirstOrDefaultAsync(ct);
        return Conflict(new { error = "Insufficient or expired stock.", shortage = true, available,
                              hint = "Use POST /api/resources/resolve-shortage for competing requests, or GET /api/resources/suggest for other locations." });
    }

    /// Shortage scenario: several urgent requests need the same limited stock.
    /// Highest priority (then oldest) is served first, the rest are recorded as 'Preempted' under one ConflictGroupId,
    /// and, optionally, a partner organisation is asked to cover the unmet quantity.
    [HttpPost("resolve-shortage")]
    public async Task<IActionResult> ResolveShortage(ShortageDto dto, CancellationToken ct)
    {
        var inv = await _db.ResourceInventories.FindAsync(new object[] { dto.InventoryId }, ct);
        if (inv is null) return NotFound(new { error = "Inventory not found." });
        var ids = dto.Requests.Select(r => r.RequestId).Distinct().ToList();
        var reqs = await _db.AssistanceRequests.Where(r => ids.Contains(r.RequestId)).ToDictionaryAsync(r => r.RequestId, ct);
        if (reqs.Count != ids.Count) return BadRequest(new { error = "One or more requests do not exist." });

        var userId = User.GetUserId(); var group = Guid.NewGuid();
        var demands = dto.Requests.Select(l => new ShortageDemand(l.RequestId, reqs[l.RequestId].FinalPriority ?? reqs[l.RequestId].UserUrgency ?? 3, reqs[l.RequestId].CreatedAt, l.Quantity));
        var plan = ShortageResolver.Resolve(inv.QuantityTotal - inv.QuantityAllocated, demands);
        var result = new List<object>(); decimal totalUnmet = 0;

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            foreach (var p in plan)
            {
                decimal granted = p.Granted, unmet = p.Unmet;
                if (granted > 0 && !await TryReserve(dto.InventoryId, granted, ct))   // stock changed in the meantime
                { unmet += granted; granted = 0; }

                if (granted > 0)
                {
                    _db.ResourceAllocations.Add(new ResourceAllocation { InventoryId = dto.InventoryId, RequestId = p.RequestId, Quantity = granted, ConflictGroupId = group, AllocatedBy = userId });
                    Workflow.MarkAssigned(_db, reqs[p.RequestId], userId, "Resources allocated (shortage resolution)");
                    Workflow.Notify(_db, p.RequestId, "RESOURCE_ALLOCATED", "Help is on the way",
                        unmet == 0 ? "Supplies have been reserved for your request." : "Part of your request has been reserved; we are sourcing the rest.");
                }
                if (unmet > 0)
                {
                    _db.ResourceAllocations.Add(new ResourceAllocation { InventoryId = dto.InventoryId, RequestId = p.RequestId, Quantity = unmet, Status = "Preempted", ConflictGroupId = group, AllocatedBy = userId });
                    totalUnmet += unmet;
                    if (granted == 0) Workflow.Notify(_db, p.RequestId, "RESOURCE_DELAYED", "Your request is queued", "Supplies are limited right now. Your request keeps its place while we source more.");
                }
                result.Add(new { p.RequestId, granted, unmet, status = unmet == 0 ? "Reserved" : granted == 0 ? "Preempted" : "PartiallyReserved" });
            }
            if (totalUnmet > 0 && dto.PartnerOrganisationId is int partner)
                _db.PartnerAssistanceRequests.Add(new PartnerAssistanceRequest { FromOrganisationId = inv.OrganisationId, ToOrganisationId = partner, ResourceTypeId = inv.ResourceTypeId, Quantity = totalUnmet });
            Workflow.Audit(_db, userId, "SHORTAGE_RESOLVED", "ResourceInventory", dto.InventoryId.ToString(), null, $"group {group}, unmet {totalUnmet}");
            await _db.SaveChangesAsync(ct);
        }, ct);

        return Ok(new { conflictGroupId = group, totalUnmet, partnerRequested = totalUnmet > 0 && dto.PartnerOrganisationId is not null, result });
    }

    [HttpGet("allocations")]
    public async Task<IActionResult> Allocations([FromQuery] long? requestId, CancellationToken ct)
    {
        var q = _db.ResourceAllocations.AsQueryable();
        if (requestId is not null) q = q.Where(a => a.RequestId == requestId);
        return Ok(await q.OrderByDescending(a => a.AllocatedAt).Take(200).ToListAsync(ct));
    }

    [HttpPatch("allocations/{id:long}/status")]
    public async Task<IActionResult> SetAllocationStatus(long id, StatusDto dto, CancellationToken ct)
    {
        var allowed = new[] { "Dispatched", "Delivered", "Cancelled" };
        if (!allowed.Contains(dto.Status)) return BadRequest(new { error = $"Status must be one of {string.Join(", ", allowed)}." });
        var a = await _db.ResourceAllocations.FindAsync(new object[] { id }, ct);
        if (a is null) return NotFound();
        if (a.Status is "Delivered" or "Cancelled" or "Preempted") return Conflict(new { error = $"Allocation is already {a.Status}." });

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            if (dto.Status == "Cancelled")                      // release the reserved stock
            {
                var q = a.Quantity;
                await _db.ResourceInventories.Where(i => i.InventoryId == a.InventoryId)
                    .ExecuteUpdateAsync(u => u.SetProperty(i => i.QuantityAllocated, i => i.QuantityAllocated - q >= 0 ? i.QuantityAllocated - q : 0), ct);
            }
            a.Status = dto.Status;
            if (dto.Status == "Delivered")
            {
                var req = await _db.AssistanceRequests.FindAsync(new object[] { a.RequestId }, ct);
                if (req is { Status: RequestStatus.Assigned }) { req.ChangeStatus(RequestStatus.InProgress); _db.RequestHistory.Add(new RequestHistory { RequestId = req.RequestId, FromStatus = "Assigned", ToStatus = "InProgress", ChangedBy = User.GetUserId(), Note = "Resources delivered" }); }
                Workflow.Notify(_db, a.RequestId, "RESOURCE_DELIVERED", "Supplies delivered", "Your supplies have been delivered.");
            }
            Workflow.Audit(_db, User.GetUserId(), "ALLOCATION_" + dto.Status.ToUpperInvariant(), "ResourceAllocation", id.ToString());
            await _db.SaveChangesAsync(ct);
        }, ct);
        return Ok();
    }

    // ---- partner organisations ----
    [HttpGet("partner-requests")]
    public async Task<IActionResult> PartnerRequests(CancellationToken ct) => Ok(await _db.PartnerAssistanceRequests.OrderByDescending(p => p.CreatedAt).Take(100).ToListAsync(ct));

    [HttpPatch("partner-requests/{id:long}/status")]
    public async Task<IActionResult> PartnerStatus(long id, StatusDto dto, CancellationToken ct)
    {
        if (!new[] { "Accepted", "Declined", "Fulfilled" }.Contains(dto.Status)) return BadRequest(new { error = "Invalid status." });
        var p = await _db.PartnerAssistanceRequests.FindAsync(new object[] { id }, ct);
        if (p is null) return NotFound();
        p.Status = dto.Status;
        Workflow.Audit(_db, User.GetUserId(), "PARTNER_REQUEST_" + dto.Status.ToUpperInvariant(), "PartnerAssistanceRequest", id.ToString());
        await _db.SaveChangesAsync(ct);
        return Ok(p);
    }

    /// Dispatch a reserved allocation. Distance and ETA come from the external routing service (estimate if it is down).
    [HttpPost("allocations/{id:long}/dispatch")]
    public async Task<IActionResult> Dispatch(long id, DispatchDto dto, CancellationToken ct)
    {
        var a = await _db.ResourceAllocations.FindAsync(new object[] { id }, ct);
        if (a is null) return NotFound();
        if (a.Status != "Reserved") return Conflict(new { error = $"Only reserved allocations can be dispatched (this one is {a.Status})." });
        var inv = await _db.ResourceInventories.FindAsync(new object[] { a.InventoryId }, ct);
        var req = await _db.AssistanceRequests.FindAsync(new object[] { a.RequestId }, ct);

        RouteInfo? route = null;
        if (inv?.Latitude is not null && inv.Longitude is not null && req?.Latitude is not null && req.Longitude is not null)
            route = await _routing.RouteAsync((double)inv.Latitude, (double)inv.Longitude, (double)req.Latitude, (double)req.Longitude, ct);

        var rec = new DispatchRecord
        {
            ResourceAllocationId = id, VehicleInventoryId = dto.VehicleInventoryId, DriverUserId = dto.DriverUserId,
            DistanceKm = route is null ? null : (decimal)route.DistanceKm, EtaMinutes = route is null ? null : (int)route.DurationMinutes,
            DispatchedAt = DateTime.UtcNow, Status = "InTransit"
        };
        _db.DispatchRecords.Add(rec);
        a.Status = "Dispatched";
        Workflow.Audit(_db, User.GetUserId(), "RESOURCE_DISPATCHED", "ResourceAllocation", id.ToString());
        if (req is not null)
            Workflow.Notify(_db, req.RequestId, "RESOURCE_DISPATCHED", "Your supplies are on the way", route is null ? "Your supplies have been dispatched." : $"Your supplies have been dispatched. Estimated arrival: about {(int)route.DurationMinutes} minutes.");
        await _db.SaveChangesAsync(ct);
        return Ok(new { rec.DispatchId, rec.DistanceKm, rec.EtaMinutes, estimated = route?.Estimated });
    }

    [HttpPatch("dispatches/{dispatchId:long}/status")]
    public async Task<IActionResult> DispatchStatus(long dispatchId, StatusDto dto, CancellationToken ct)
    {
        if (!new[] { "Delivered", "Failed" }.Contains(dto.Status)) return BadRequest(new { error = "Status must be Delivered or Failed." });
        var d = await _db.DispatchRecords.FindAsync(new object[] { dispatchId }, ct);
        if (d is null) return NotFound();
        if (d.Status is "Delivered" or "Failed") return Conflict(new { error = $"Dispatch is already {d.Status}." });
        var a = await _db.ResourceAllocations.FindAsync(new object[] { d.ResourceAllocationId }, ct);
        d.Status = dto.Status;
        if (dto.Status == "Delivered")
        {
            d.DeliveredAt = DateTime.UtcNow;
            if (a is not null)
            {
                a.Status = "Delivered";
                var req = await _db.AssistanceRequests.FindAsync(new object[] { a.RequestId }, ct);
                if (req is { Status: RequestStatus.Assigned }) { req.ChangeStatus(RequestStatus.InProgress); _db.RequestHistory.Add(new RequestHistory { RequestId = req.RequestId, FromStatus = "Assigned", ToStatus = "InProgress", ChangedBy = User.GetUserId(), Note = "Resources delivered" }); }
                Workflow.Notify(_db, a.RequestId, "RESOURCE_DELIVERED", "Supplies delivered", "Your supplies have been delivered.");
            }
        }
        else if (a is not null) a.Status = "Reserved";          // failed delivery: stock stays reserved so it can be dispatched again
        Workflow.Audit(_db, User.GetUserId(), "DISPATCH_" + dto.Status.ToUpperInvariant(), "DispatchRecord", dispatchId.ToString());
        await _db.SaveChangesAsync(ct);
        return Ok();
    }
}
