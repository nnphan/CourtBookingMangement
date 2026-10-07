using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Permissions.Models.Requests;
using CourtBookingManagement.Application.Permissions.Models.Responses;
using CourtBookingManagement.Application.Permissions.Services;
using CourtBookingManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/permissions")]
public sealed class PermissionsController(IPermissionService service) : ApiControllerBase
{
    [HttpGet]
    [Permission("permission.view")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPermissions(
        [FromQuery] PermissionSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.SearchAsync(request, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value.Items, "Success", result.Value.Metadata)
            : Error(result.Error);
    }
}