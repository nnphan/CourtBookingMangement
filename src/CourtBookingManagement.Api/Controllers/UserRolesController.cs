using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.UserRoles.Models.Responses;
using CourtBookingManagement.Application.UserRoles.Services;
using CourtBookingManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UserRolesController(IUserRoleService service) : ApiControllerBase
{
    [HttpGet("{id:guid}/roles")]
    [Permission("role.assign")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserRoleResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserRoles(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetUserRolesAsync(id, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value, "User roles retrieved successfully.")
            : Error(result.Error);
    }
}
