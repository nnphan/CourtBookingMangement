using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Application.Matching.Services;
using CourtBookingManagement.Domain.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me/match-join-requests")]
public sealed class MyMatchJoinRequestsController(
    IPlayerMatchService service,
    ICurrentUserService currentUserService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<MyMatchJoinRequestResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(
        [FromQuery] GetMyMatchJoinRequestsRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.GetMyJoinRequestsAsync(request, userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value.Items, "Join request history retrieved successfully.", result.Value.Metadata)
            : Error(result.Error);
    }
}
