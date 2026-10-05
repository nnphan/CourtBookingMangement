using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Branches.DTOs;
using CourtBookingManagement.Application.Branches.GetBranchDetails;
using CourtBookingManagement.Application.CourtStatus.DTOs;
using CourtBookingManagement.Application.CourtStatus.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/branches")]
public sealed class BranchesController(ICourtStatusService service, ISender sender) : ApiControllerBase
{
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