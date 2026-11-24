using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.StaffManagement.DTOs;
using CourtBookingManagement.Application.StaffManagement.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CourtBookingManagement.Api.Controllers;

/// <summary>Validates and redeems email-bound Staff invitations.</summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("staff-activation")]
[Route("api/auth/staff-invitations")]
public sealed class StaffInvitationsController(IStaffCreationService service) : ApiControllerBase
{
    /// <summary>Returns safe details for a valid, unexpired Staff invitation.</summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ApiResponse<StaffInvitationValidationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Validate(
        [FromBody] ValidateStaffInvitationRequest request,
        CancellationToken cancellationToken) =>
        FromResult(
            await service.ValidateInvitationAsync(request, cancellationToken),
            "Invitation is valid.");

    /// <summary>Sets the password and atomically activates an email-bound Staff invitation.</summary>
    [HttpPost("activate")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Activate(
        [FromBody] ActivateStaffInvitationRequest request,
        CancellationToken cancellationToken) =>
        FromResult(
            await service.ActivateInvitationAsync(request, cancellationToken),
            "Staff account activated.");
}
