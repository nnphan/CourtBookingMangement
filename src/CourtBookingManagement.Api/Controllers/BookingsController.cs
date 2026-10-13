using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.CourtStatus.DTOs;
using CourtBookingManagement.Application.CourtStatus.Interfaces;
using CourtBookingManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using BookingCreateRequest = CourtBookingManagement.Application.Bookings.DTOs.CreateBookingRequest;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public sealed class BookingsController(ICourtStatusService courtStatusService, IBookingService bookingService) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => FromResult(await courtStatusService.GetBookingAsync(id, ct));

    [HttpPost]
    //[Permission("booking.create")]
    public async Task<IActionResult> Create([FromBody] BookingCreateRequest request, CancellationToken ct)
    {
        var result = await bookingService.CreateAsync(request, ct);
        return result.IsSuccess
            ? Created(result.Value, nameof(Get), new { id = result.Value.BookingId }, "Booking created successfully.")
            : Error(result.Error);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateBookingRequest request, CancellationToken ct) => FromResult(await courtStatusService.UpdateBookingAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) => FromResult(await courtStatusService.CancelBookingAsync(id, ct), "Booking cancelled successfully.");
}