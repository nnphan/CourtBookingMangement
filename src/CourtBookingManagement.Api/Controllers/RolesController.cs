using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Roles.Models.Requests;
using CourtBookingManagement.Application.Roles.Models.Responses;
using CourtBookingManagement.Application.Roles.Services;
using CourtBookingManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/roles")]
public sealed class RolesController(IRoleService service) : ApiControllerBase
{
    [HttpGet]
    [Permission("role.view")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRoles(
        [FromQuery] RoleSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.SearchAsync(request, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value.Items, "Roles retrieved successfully.", result.Value.Metadata)
            : Error(result.Error);
    }
}