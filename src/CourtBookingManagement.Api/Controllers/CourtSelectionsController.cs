using CourtBookingManagement.Api.Realtime;
using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/court-selections")]
public sealed class CourtSelectionsController(
    CourtSelectionRepository repository, CourtSelectionIdentity identity,
    CourtSelectionPublisher publisher, ILogger<CourtSelectionsController> logger) : ApiControllerBase
{
    [HttpGet("{branchId:guid}/{businessDate}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(Guid branchId, DateOnly businessDate, Guid? ownerSessionId,
        [FromHeader(Name = CourtSelectionIdentity.GuestHeader)] string? guestToken, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        var owner = identity.Resolve(ownerSessionId, guestToken);
        var snapshot = await repository.ReadAsync(branchId, businessDate, owner?.SubjectId, ownerSessionId, ct);
        return snapshot.TimeZone is null
            ? NotFound(new { code = "BRANCH_ACCESS_DENIED", message = "Không thể xem lịch của chi nhánh này." })
            : Success(snapshot, "Court selection snapshot retrieved.");
    }

    [HttpPost("guest-session")]
    [AllowAnonymous]
    [EnableRateLimiting("court-selection")]
    public IActionResult GuestSession([FromQuery] Guid ownerSessionId)
    {
        if (ownerSessionId == Guid.Empty) return BadRequest();
        Response.Headers.CacheControl = "private, no-store";
        return Success(identity.Issue(ownerSessionId), "Court selection session issued.");
    }

    [HttpPost("slots")]
    [AllowAnonymous]
    [EnableRateLimiting("court-selection")]
    public Task<IActionResult> Claim(CourtSelectionRequest request, [FromHeader(Name = "Idempotency-Key")] Guid key,
        [FromHeader(Name = CourtSelectionIdentity.GuestHeader)] string? guestToken, CancellationToken ct) =>
        MutateAsync(request, key, guestToken, false, ct);

    [HttpDelete("{holdId:guid}")]
    [HttpDelete("{holdId:guid}/slots/{slotId:guid}")]
    [AllowAnonymous]
    [EnableRateLimiting("court-selection")]
    public Task<IActionResult> Release(Guid holdId, Guid? slotId, [FromQuery] Guid branchId,
        [FromQuery] DateOnly businessDate, [FromQuery] Guid ownerSessionId,
        [FromHeader(Name = "Idempotency-Key")] Guid key,
        [FromHeader(Name = CourtSelectionIdentity.GuestHeader)] string? guestToken, CancellationToken ct) =>
        MutateAsync(new(branchId, businessDate, ownerSessionId, Guid.Empty, default, default, holdId, slotId), key, guestToken, true, ct);

    private async Task<IActionResult> MutateAsync(CourtSelectionRequest request, Guid key, string? guestToken, bool release, CancellationToken ct)
    {
        if (key == Guid.Empty || request.OwnerSessionId == Guid.Empty || request.BranchId == Guid.Empty ||
            request.BusinessDate == default || (!release && (request.CourtId == Guid.Empty ||
                request.EndTime <= request.StartTime || request.EndTime - request.StartTime > TimeSpan.FromHours(1)
                || request.StartTime.Ticks % TimeSpan.TicksPerMinute != 0 || request.EndTime.Ticks % TimeSpan.TicksPerMinute != 0)))
            return BadRequest(new { code = "INVALID_SLOT_SELECTION", message = "Khung giờ được chọn không hợp lệ." });

        var owner = identity.Resolve(request.OwnerSessionId, guestToken);
        if (owner is null) return Unauthorized();
        var result = await repository.MutateAsync(owner, key, request, release, ct);
        CourtSelectionMetrics.Mutations.Add(1,
            new KeyValuePair<string, object?>("operation", release ? "release" : "claim"),
            new KeyValuePair<string, object?>("outcome", result.Code ?? "success"));
        logger.LogInformation("Court selection {Operation} {Outcome} for {UserId} and {HoldId}/{SlotId}, request {RequestId}",
            release ? "release" : "claim", result.Code ?? "success", owner.UserId, result.HoldId, result.SlotId, HttpContext.TraceIdentifier);
        if (result.Code is not null)
            return StatusCode(result.Code switch
            {
                "SLOT_NOT_FOUND" or "HOLD_NOT_FOUND" => StatusCodes.Status404NotFound,
                "HOLD_OWNERSHIP_MISMATCH" => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status409Conflict
            }, new
            {
                code = result.Code,
                message = result.Code == "SLOT_NOT_AVAILABLE" ? "Sân vừa được người khác chọn." : "Không thể cập nhật khung giờ đã chọn."
            });

        await publisher.PublishAsync(new(request.BranchId, request.BusinessDate),
            release ? CourtSelectionPublisher.SlotReleased : CourtSelectionPublisher.SlotHeld);
        return Success(result, "Court selection updated.");
    }
}
