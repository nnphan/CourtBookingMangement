using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.StaffManagement.DTOs;
using CourtBookingManagement.Application.StaffManagement.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/auth/staff-activation")]
public sealed class StaffActivationController(IStaffCreationService service) : ApiControllerBase
{
    /// <summary>Redeems a legacy version-1 invitation and sets the Staff account password.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("staff-activation")]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Activate([FromBody] ActivateStaffRequest request, CancellationToken cancellationToken) =>
        FromResult(await service.ActivateAsync(request, cancellationToken), "Staff account activated.");
}
