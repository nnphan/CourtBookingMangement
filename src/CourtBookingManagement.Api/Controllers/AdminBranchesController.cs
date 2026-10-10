using CourtBookingManagement.Api.Common.Constants;
using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Admin.Branches.DTOs;
using CourtBookingManagement.Application.Admin.Branches.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
//[Authorize(Policy = Permissions.BranchView)]
[Route("api/admin/branches")]
public sealed class AdminBranchesController(IAdminBranchService service) : ApiControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<BranchSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var result = await service.GetSummaryAsync(cancellationToken);
        return Success(result, "Success");
    }

    /// <summary>Returns a filtered, paginated list of branches for administration.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BranchListResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBranches(
        [FromQuery] GetBranchesRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.GetBranchesAsync(request, cancellationToken);
        return Success(result.Items, "Success", result.Metadata);
    }
}