using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.CourtBookings.DTOs;
using CourtBookingManagement.Application.CourtBookings.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/court-bookings")]
public sealed class CourtBookingsController(ICourtBookingService service) : ApiControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateCourtBookingRequest request, CancellationToken cancellationToken) =>
        FromResult(await service.CreateAsync(request, cancellationToken), "Booking created successfully");

    [HttpPost("check-availability")]
    public async Task<IActionResult> CheckAvailability(CheckCourtAvailabilityRequest request, CancellationToken cancellationToken) =>
        FromResult(await service.CheckAvailabilityAsync(request, cancellationToken));

    [HttpPost("search")]
    public async Task<IActionResult> Search(SearchCourtBookingsRequest request, CancellationToken cancellationToken) =>
        FromResult(await service.SearchAsync(request, cancellationToken));

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(CancelCourtBookingRequest request, CancellationToken cancellationToken) =>
        FromResult(await service.CancelAsync(request, cancellationToken), "Booking cancelled successfully");
}