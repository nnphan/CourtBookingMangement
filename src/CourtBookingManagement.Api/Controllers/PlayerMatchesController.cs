using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Application.Matching.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/player-matches")]
public sealed class PlayerMatchesController(IPlayerMatchService service, ICurrentUserService currentUserService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] PlayerMatchSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetMatchesAsync(request, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value.Items, "Success", result.Value.Metadata)
            : Error(result.Error);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PlayerMatchDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetMatchByIdAsync(id, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Player match retrieved successfully.")
            : Error(result.Error);
    }

    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreatePlayerMatchResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreatePlayerMatchRequest request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.CreateAsync(request, userId, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, ApiResponse<CreatePlayerMatchResponse>.Create(result.Value, "Player match created successfully.", HttpContext.TraceIdentifier))
            : Error(result.Error);
    }

    [Authorize]
    [HttpPost("{id:guid}/join")]
    [ProducesResponseType(typeof(ApiResponse<JoinMatchResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> JoinMatch(Guid id, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.JoinMatchAsync(id, userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Join request submitted successfully.")
            : Error(result.Error);
    }

    [Authorize]
    [HttpPost("{id:guid}/join-requests/{requestId:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<ApproveJoinRequestResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveJoinRequest(Guid id, Guid requestId, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.ApproveJoinRequestAsync(id, requestId, userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Join request approved successfully.")
            : Error(result.Error);
    }
}
