using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Application.Bookings.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/booking-holds")]
[AllowAnonymous]
public sealed class BookingHoldsController(IBookingService bookingService) : ApiControllerBase
{
    [HttpGet("{bookingId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BookingDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        Guid bookingId,
        [FromHeader(Name = "X-Booking-Hold-Token")] string? holdToken,
        CancellationToken cancellationToken) =>
        FromResult(
            await bookingService.GetHoldAsync(bookingId, holdToken, cancellationToken),
            "Booking hold retrieved successfully.");

    [HttpDelete("{bookingId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CancelBookingHoldResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid bookingId,
        [FromHeader(Name = "X-Booking-Hold-Token")] string? holdToken,
        CancellationToken cancellationToken) =>
        FromResult(
            await bookingService.CancelHoldAsync(bookingId, holdToken, cancellationToken),
            "Booking hold cancelled successfully.");
}
