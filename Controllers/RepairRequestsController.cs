using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairConnect.Data;
using RepairConnect.Models;
using RepairConnect.Models.Enums;

namespace RepairConnect.Controllers;

[Route("api/repair-requests")]
[Authorize(Policy = "ClientOnly")]
public sealed class RepairRequestsController(RepairConnectDbContext db) : ApiControllerBase
{
    public sealed record RequestInput(int? CategoryId, [Required, StringLength(150)] string Title,
        [Required, StringLength(4000)] string Description, [Required, StringLength(300)] string ServiceLocation,
        RepairUrgency Urgency, DateOnly? PreferredDate, int[]? CategoryIds = null);

    [HttpPost]
    public async Task<IActionResult> Create(RequestInput input, CancellationToken ct)
    {
        var ids = input.CategoryIds?.Distinct().ToArray() ?? (input.CategoryId is int single ? [single] : Array.Empty<int>());
        if (ids.Length == 0 || !Enum.IsDefined(input.Urgency) ||
            await db.ServiceCategories.CountAsync(c => ids.Contains(c.Id) && c.IsActive, ct) != ids.Length)
            return BadRequest(new { detail = "Choose available services and a valid urgency." });

        var requests = ids.Select(id => new RepairRequest
        {
            ClientUserId = CurrentUserId, CategoryId = id, Title = input.Title.Trim(),
            Description = input.Description.Trim(), ServiceLocation = input.ServiceLocation.Trim(),
            Urgency = input.Urgency, PreferredDate = input.PreferredDate
        }).ToArray();
        db.RepairRequests.AddRange(requests);
        await db.SaveChangesAsync(ct);
        return Created($"/Dashboard#request-{requests[0].Id}", new { id = requests[0].Id, ids = requests.Select(r => r.Id) });
    }

    public sealed record AcceptInput(DateOnly? ScheduledDate);

    [HttpPost("{id:int}/accept")]
    [Authorize(Policy = "TechnicianOnly")]
    public async Task<IActionResult> Accept(int id, AcceptInput input, CancellationToken ct)
    {
        var technicianId = CurrentUserId;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var updated = await db.RepairRequests.Where(r => r.Id == id && r.ClientUserId != technicianId &&
            r.Status == RepairRequestStatus.Open && db.TechnicianCategories.Any(c =>
                c.TechnicianUserId == technicianId && c.CategoryId == r.CategoryId && c.ReviewStatus == null && c.Technician.Status == VerificationStatus.Approved))
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.Status, RepairRequestStatus.Booked), ct);
        if (updated == 0) return Conflict(new { detail = "This request is no longer available to you." });

        db.Bookings.Add(new Booking { RepairRequestId = id, TechnicianUserId = technicianId,
            Status = BookingStatus.Accepted, AssignedAt = DateTime.UtcNow,
            ScheduledDate = input.ScheduledDate?.ToDateTime(TimeOnly.MinValue) });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:int}/start")]
    [Authorize(Policy = "TechnicianOnly")]
    public Task<IActionResult> Start(int id, CancellationToken ct) =>
        Advance(id, BookingStatus.Accepted, BookingStatus.InProgress, ct);

    [HttpPost("{id:int}/complete")]
    [Authorize(Policy = "TechnicianOnly")]
    public Task<IActionResult> Complete(int id, CancellationToken ct) =>
        Advance(id, BookingStatus.InProgress, BookingStatus.Completed, ct);

    private async Task<IActionResult> Advance(int id, BookingStatus from, BookingStatus to, CancellationToken ct)
    {
        var technicianId = CurrentUserId;
        var booking = db.Bookings.Where(b => b.RepairRequestId == id && b.TechnicianUserId == technicianId && b.Status == from);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var changed = to == BookingStatus.Completed
            ? await booking.ExecuteUpdateAsync(set => set.SetProperty(b => b.Status, to)
                .SetProperty(b => b.CompletedAt, now).SetProperty(b => b.EndedAt, now), ct)
            : await booking.ExecuteUpdateAsync(set => set.SetProperty(b => b.Status, to), ct);
        if (changed == 0) return Conflict(new { detail = "This booking is no longer available for that action." });
        if (to == BookingStatus.Completed)
            await db.RepairRequests.Where(r => r.Id == id).ExecuteUpdateAsync(set => set.SetProperty(r => r.Status, RepairRequestStatus.Completed), ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    public sealed record ReviewInput([Range(1, 5)] int Rating, [StringLength(2000)] string? Comment);

    [HttpPost("{id:int}/review")]
    public async Task<IActionResult> Review(int id, ReviewInput input, CancellationToken ct)
    {
        var userId = CurrentUserId;
        var booking = await db.Bookings.SingleOrDefaultAsync(b => b.RepairRequestId == id &&
            b.RepairRequest.ClientUserId == userId && b.Status == BookingStatus.Completed, ct);
        if (booking == null) return NotFound();
        db.Reviews.Add(new Review { BookingId = booking.Id, Rating = (byte)input.Rating, Comment = input.Comment?.Trim() });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) when (!ct.IsCancellationRequested)
        {
            if (await db.Reviews.AnyAsync(r => r.BookingId == booking.Id, ct))
                return Conflict(new { detail = "You already reviewed this repair." });
            throw;
        }
        return NoContent();
    }
}
