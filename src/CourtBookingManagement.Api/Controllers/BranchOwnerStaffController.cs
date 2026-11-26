using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.StaffManagement.DTOs;
using CourtBookingManagement.Application.StaffManagement.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CourtBookingManagement.Api.Controllers;

/// <summary>Provides owner-scoped Staff list and detail endpoints.</summary>
[ApiController]
[Authorize]
[Route("api/branch-owner/staff")]
public sealed class BranchOwnerStaffController(
    IBranchOwnerStaffService service,
    IStaffCreationService creationService,
    ICurrentUserService currentUserService) : ApiControllerBase
{
    /// <summary>Creates a Staff account and sends a one-time activation invitation.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreateStaffResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create([FromBody] CreateStaffRequest request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid actorId)
        {
            return Unauthorized();
        }

        var result = await creationService.CreateAsync(actorId, request, cancellationToken);
        return result.IsSuccess
            ? Created(result.Value, nameof(GetById), new { userId = result.Value.UserId }, "Staff created.")
            : Error(result.Error);
    }

    /// <summary>Reissues an activation invitation for a pending Staff account.</summary>
    [HttpPost("{userId:guid}/send-activation")]
    [EnableRateLimiting("staff-resend")]
    [ProducesResponseType(typeof(ApiResponse<CreateStaffResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SendActivation(Guid userId, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid actorId)
        {
            return Unauthorized();
        }

        var result = await creationService.ResendAsync(actorId, userId, cancellationToken);
        return result.IsSuccess ? Success(result.Value, "Invitation processed.") : Error(result.Error);
    }

    /// <summary>Returns a filtered page of Staff assigned to branches owned by the caller.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffListItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetList(
        [FromQuery] StaffListRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid ownerUserId)
        {
            return Unauthorized();
        }

        var result = await service.GetListAsync(ownerUserId, request, cancellationToken);
        if (result.IsFailure && request.BranchId is null && IsUnassignedOwner(result.Error.Code))
        {
            var empty = PagedResult<StaffListItemResponse>.Create([], request.Page, request.PageSize, 0);
            return Success(empty.Items, "No owned branches are configured.", empty.Metadata);
        }

        return result.IsSuccess
            ? Success(result.Value.Items, "Staff retrieved successfully.", result.Value.Metadata)
            : Error(result.Error);
    }

    /// <summary>Returns active owned branches available to the Staff list filter.</summary>
    [HttpGet("branches")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<StaffOwnedBranchResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBranches(CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid ownerUserId)
        {
            return Unauthorized();
        }

        var result = await service.GetBranchesAsync(ownerUserId, cancellationToken);
        if (result.IsFailure && IsUnassignedOwner(result.Error.Code))
            return Success(Array.Empty<StaffOwnedBranchResponse>(), "No owned branches are configured.");

        return result.IsSuccess
            ? Success(result.Value, "Staff branches retrieved successfully.")
            : Error(result.Error);
    }

    private bool IsUnassignedOwner(string errorCode) =>
        (errorCode is "STAFF_OWNER_NOT_FOUND" or "STAFF_BRANCH_ACCESS_DENIED") &&
        currentUserService.Roles.Contains("BRANCH_OWNER", StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns one Staff member and assignments visible to the caller.</summary>
    [HttpGet("{userId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<StaffDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid ownerUserId)
        {
            return Unauthorized();
        }

        var result = await service.GetByIdAsync(ownerUserId, userId, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "Staff retrieved successfully.")
            : Error(result.Error);
    }
}
