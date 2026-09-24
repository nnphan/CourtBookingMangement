using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.CourtStatus.DTOs;
using CourtBookingManagement.Application.CourtStatus.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public sealed class BookingsController(ICourtStatusService service) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => FromResult(await service.GetBookingAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateBookingRequest request, CancellationToken ct)
    {
        var result = await service.CreateBookingAsync(request, ct);
        return result.IsSuccess ? Created(result.Value, nameof(Get), new { id = result.Value.BookingId }) : Error(result.Error);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateBookingRequest request, CancellationToken ct) => FromResult(await service.UpdateBookingAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) => FromResult(await service.CancelBookingAsync(id, ct), "Booking cancelled successfully.");
}