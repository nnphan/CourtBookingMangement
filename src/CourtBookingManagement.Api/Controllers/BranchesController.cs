using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.CourtStatus.DTOs;
using CourtBookingManagement.Application.CourtStatus.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/branches")]
public sealed class BranchesController(ICourtStatusService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] BranchSearchRequest request, CancellationToken ct)
    {
        var result = await service.SearchBranchesAsync(request, ct);
        return result.IsSuccess
            ? Success(result.Value.Items, "Success", result.Value.Metadata)
            : Error(result.Error);
    }
}