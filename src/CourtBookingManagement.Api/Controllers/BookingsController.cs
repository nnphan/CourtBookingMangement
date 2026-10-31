using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Api.Common.Constants;
using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.CourtStatus.DTOs;
using CourtBookingManagement.Application.CourtStatus.Interfaces;
using CourtBookingManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BookingCreateRequest = CourtBookingManagement.Application.Bookings.DTOs.CreateBookingRequest;
using BookingDetailResponseDto = CourtBookingManagement.Application.Bookings.DTOs.BookingDetailResponse;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public sealed class BookingsController(ICourtStatusService courtStatusService, IBookingService bookingService) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BookingDetailResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        FromResult(await bookingService.GetByIdAsync(id, ct), "Booking retrieved successfully.");

    [HttpPatch("{id:guid}")]
    //[Permission(Permissions.BookingStatusUpdate)]
    [ProducesResponseType(typeof(ApiResponse<UpdateBookingStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateBookingStatusRequest request,
        CancellationToken ct) =>
        FromResult(
            await bookingService.UpdateStatusAsync(id, request, ct),
            "Booking status updated successfully.");

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<CreateBookingResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] BookingCreateRequest request, CancellationToken ct)
    {
        var result = await bookingService.CreateAsync(request, ct);
        return result.IsSuccess
            ? Created(result.Value, nameof(Get), new { id = result.Value.BookingId }, "Booking created successfully.")
            : Error(result.Error);
    }

    [HttpPost("calculate-price")]
    //[Permission("booking.create")]
    [ProducesResponseType(typeof(ApiResponse<CalculateBookingPriceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CalculatePrice(
        [FromBody] CalculateBookingPriceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await bookingService.CalculatePriceAsync(request, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Price calculated successfully.")
            : Error(result.Error);
    }

    [HttpGet("my-bookings")]
    //[Authorize(Roles = "CUSTOMER")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MyBookingResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyBookings(CancellationToken cancellationToken)
    {
        var result = await bookingService.GetMyBookingsAsync(cancellationToken);
        return FromResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateBookingRequest request, CancellationToken ct) => FromResult(await courtStatusService.UpdateBookingAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) => FromResult(await courtStatusService.CancelBookingAsync(id, ct), "Booking cancelled successfully.");
}