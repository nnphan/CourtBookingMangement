using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Application.Matching.Services;
using CourtBookingManagement.Domain.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public sealed class PlayerMatchingProfileController(
    IPlayerMatchingProfileService service,
    ICurrentUserService currentUserService) : ApiControllerBase
{
    [HttpGet("player-matching-profile")]
    [ProducesResponseType(typeof(ApiResponse<PlayerMatchingProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.GetProfileAsync(userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Player-matching profile retrieved successfully.")
            : Error(result.Error);
    }

    [HttpPut("player-matching-profile")]
    [ProducesResponseType(typeof(ApiResponse<PlayerMatchingProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdatePlayerMatchingProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.UpdateProfileAsync(request, userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Player-matching profile updated successfully.")
            : Error(result.Error);
    }

    [HttpGet("match-preferences")]
    [ProducesResponseType(typeof(ApiResponse<PlayerMatchPreferencesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.GetPreferencesAsync(userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Match preferences retrieved successfully.")
            : Error(result.Error);
    }

    [HttpPut("match-preferences")]
    [ProducesResponseType(typeof(ApiResponse<PlayerMatchPreferencesResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] UpdatePlayerMatchPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Error(new Error("PLAYER_MATCH.INVALID_USER", "A valid authenticated user is required."));
        }

        var result = await service.UpdatePreferencesAsync(request, userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Match preferences updated successfully.")
            : Error(result.Error);
    }
}
