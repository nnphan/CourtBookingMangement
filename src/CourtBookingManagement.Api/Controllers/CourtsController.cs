using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Courts.Models.Requests;
using CourtBookingManagement.Application.Courts.Models.Responses;
using CourtBookingManagement.Application.Courts.Services;
using CourtBookingManagement.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/courts")]
public sealed class CourtsController(
    ICourtService service,
    ICurrentUserService currentUserService) : ApiControllerBase
{
    [HttpGet]
    [Permission("court.view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CourtListItemResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCourts(
        [FromQuery] CourtSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.SearchAsync(request, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value.Items, "Courts retrieved successfully.", result.Value.Metadata)
            : Error(result.Error);
    }

    [HttpGet("{id:guid}")]
    [Permission("court.view")]
    [ProducesResponseType(typeof(ApiResponse<CourtDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return FromResult(result, "Court retrieved successfully.");
    }

    [HttpPost]
    [Permission("court.create")]
    [ProducesResponseType(typeof(ApiResponse<CourtDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCourtRequestDTO request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.CreateAsync(request, userId, cancellationToken);
        return result.IsSuccess
            ? Created(result.Value, nameof(GetById), new { id = result.Value.Id }, "Court created successfully.")
            : Error(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Permission("court.update")]
    [ProducesResponseType(typeof(ApiResponse<CourtDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCourtRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.UpdateAsync(id, request, userId, cancellationToken);
        return FromResult(result, "Court updated successfully.");
    }

    [HttpDelete("{id:guid}")]
    [Permission("court.delete")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await service.DeleteAsync(id, userId, cancellationToken);
        return FromResult(result, "Court deleted successfully.");
    }
}
