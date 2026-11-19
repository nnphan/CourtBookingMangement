using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Application.Matching.Services;
using CourtBookingManagement.Domain.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/player-matches")]
public sealed class PlayerMatchesController(IPlayerMatchService service, ICurrentUserService currentUserService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PlayerMatchResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] PlayerMatchSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetMatchesAsync(request, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value.Items, "Success", result.Value.Metadata)
            : Error(result.Error);
    }

    [Authorize]
    [HttpPost("search")]
    [ProducesResponseType(typeof(ApiResponse<PersonalizedPlayerMatchSearchResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SearchPersonalized(
        [FromBody] PersonalizedPlayerMatchSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.SearchPersonalizedAsync(request, userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value.Data, "Personalized player matches retrieved successfully.", result.Value.Metadata)
            : Error(result.Error);
    }

    [Authorize]
    [HttpPost("{id:guid}/score")]
    [ProducesResponseType(typeof(ApiResponse<PersonalizedPlayerMatchResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ScoreMatch(
        Guid id,
        [FromBody] PlayerMatchScoringCriteriaRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.ScoreMatchAsync(id, request, userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Player match scored successfully.")
            : Error(result.Error);
    }

    [Authorize]
    [HttpGet("hosted")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<HostedPlayerMatchResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetHostedMatches([FromQuery] GetHostedMatchesRequest request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.GetHostedMatchesAsync(request, userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value.Items, "Hosted player matches retrieved successfully.", result.Value.Metadata)
            : Error(result.Error);
    }

    [Authorize]
    [HttpGet("{matchId:guid}/join-requests")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PlayerMatchJoinRequestResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJoinRequests(
        [FromRoute] Guid matchId,
        [FromQuery] GetPlayerMatchJoinRequestsRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.GetJoinRequestsAsync(matchId, request, userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Join requests retrieved successfully.")
            : Error(result.Error);
    }

    [Authorize]
    [HttpGet("participating")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ParticipatingPlayerMatchResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetParticipatingMatches([FromQuery] GetParticipatingMatchesRequest request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.GetParticipatingMatchesAsync(request, userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value.Items, "Participating player matches retrieved successfully.", result.Value.Metadata)
            : Error(result.Error);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PlayerMatchDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetMatchByIdAsync(id, currentUserService.UserId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Player match retrieved successfully.")
            : Error(result.Error);
    }

    [Authorize]
    [HttpPost("drafts")]
    [ProducesResponseType(typeof(ApiResponse<PlayerMatchLifecycleResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateDraft([FromBody] PlayerMatchDraftRequest request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.CreateDraftAsync(request, userId, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(
                nameof(GetById),
                new { id = result.Value.Id },
                ApiResponse<PlayerMatchLifecycleResponse>.Create(
                    result.Value, "Player match draft created successfully.", HttpContext.TraceIdentifier))
            : Error(result.Error);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PlayerMatchLifecycleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] PlayerMatchDraftRequest request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.UpdateDraftAsync(id, request, userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Player match draft updated successfully.")
            : Error(result.Error);
    }

    [Authorize]
    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(typeof(ApiResponse<PlayerMatchLifecycleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PublishDraft(Guid id, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.PublishDraftAsync(id, userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Player match published successfully.")
            : Error(result.Error);
    }

    [Authorize]
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<PlayerMatchLifecycleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelMatch(Guid id, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.CancelMatchAsync(id, userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Player match cancelled successfully.")
            : Error(result.Error);
    }

    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreatePlayerMatchResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreatePlayerMatchRequest request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
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
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> JoinMatch(Guid id, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.JoinMatchAsync(id, userId, null, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Join request submitted successfully.")
            : Error(result.Error);
    }

    [Authorize]
    [HttpPost("{id:guid}/join-requests")]
    [ProducesResponseType(typeof(ApiResponse<JoinMatchResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateJoinRequest(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] JoinMatchRequest? request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.JoinMatchAsync(id, userId, request?.Message, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Join request submitted successfully.")
            : Error(result.Error);
    }

    [Authorize]
    [HttpDelete("{id:guid}/join-requests/me")]
    [ProducesResponseType(typeof(ApiResponse<JoinMatchResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelJoinRequest(Guid id, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.CancelJoinRequestAsync(id, userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Join request cancelled successfully.")
            : Error(result.Error);
    }

    [Authorize]
    [HttpPost("{id:guid}/join-requests/{requestId:guid}/approve")]
    [ProducesResponseType(typeof(ApiResponse<ApproveJoinRequestResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveJoinRequest(Guid id, Guid requestId, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.ApproveJoinRequestAsync(id, requestId, userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Join request approved successfully.")
            : Error(result.Error);
    }

    [Authorize]
    [HttpPost("{id:guid}/join-requests/{requestId:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<RejectJoinRequestResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectJoinRequest(
        Guid id,
        Guid requestId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RejectJoinRequestRequest? request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.RejectJoinRequestAsync(id, requestId, userId, request?.Reason, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Join request rejected successfully.")
            : Error(result.Error);
    }
}
