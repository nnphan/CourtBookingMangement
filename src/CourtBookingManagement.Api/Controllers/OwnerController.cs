using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Owners.Models;
using CourtBookingManagement.Application.Owners.Services;
using CourtBookingManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/owners")]
public sealed class OwnerController(IOwnerService service, ICurrentUserService currentUserService) : ApiControllerBase
{
    [HttpGet]
    //[Permission("owner.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OwnerResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetOwners([FromQuery] OwnerSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await service.SearchAsync(request, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value.Items, "Owners retrieved successfully.", result.Value.Metadata)
            : Error(result.Error);
    }

    [HttpGet("{id:guid}")]
    //[Permission("owner.view")]
    [ProducesResponseType(typeof(ApiResponse<OwnerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        FromResult(await service.GetByIdAsync(id, cancellationToken), "Owner retrieved successfully.");

    [HttpPost]
    //[Permission("owner.create")]
    [ProducesResponseType(typeof(ApiResponse<OwnerResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateOwnerRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, currentUserService.UserId, cancellationToken);
        return result.IsSuccess
            ? Created(result.Value, nameof(GetById), new { id = result.Value.Id }, "Owner created successfully.")
            : Error(result.Error);
    }

    [HttpPut("{id:guid}")]
   //[Permission("owner.update")]
    [ProducesResponseType(typeof(ApiResponse<OwnerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateOwnerRequest request,
        CancellationToken cancellationToken) =>
        FromResult(
            await service.UpdateAsync(id, request, currentUserService.UserId, cancellationToken),
            "Owner updated successfully.");

    [HttpDelete("{id:guid}")]
    //[Permission("owner.delete")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        FromResult(
            await service.DeleteAsync(id, currentUserService.UserId, cancellationToken),
            "Owner deleted successfully.");
}
