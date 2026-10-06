using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Branches.DTOs;
using CourtBookingManagement.Application.Branches.GetBranchDetails;
using CourtBookingManagement.Application.CourtStatus.DTOs;
using CourtBookingManagement.Application.CourtStatus.Interfaces;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Branches.DTOs.Requests;
using CourtBookingManagement.Application.Branches.DTOs.Responses;
using CourtBookingManagement.Application.Branches.Services;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/branches")]
public sealed class BranchesController(
    ICourtStatusService service,
    ISender sender,
    IBranchService branchService,
    ICurrentUserService currentUserService) : ApiControllerBase
{
    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreateBranchResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBranchRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await branchService.CreateAsync(request, userId, cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<CreateBranchResponse>.Create(
                result,
                "Branch created successfully.",
                HttpContext.TraceIdentifier));
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] BranchSearchRequest request, CancellationToken ct)
    {
        var result = await service.SearchBranchesAsync(request, ct);
        return result.IsSuccess
            ? Success(result.Value.Items, "Success", result.Value.Metadata)
            : Error(result.Error);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BranchDetailsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetBranchDetailsQuery(id), cancellationToken);
        return FromResult(result, "Branch details retrieved successfully.");
    }
}