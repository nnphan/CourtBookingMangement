using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.CourtStatus.DTOs;
using CourtBookingManagement.Application.CourtStatus.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/court-status")]
public sealed class CourtStatusController(ICourtStatusService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBoard([FromQuery] GetCourtStatusRequest request, CancellationToken ct) =>
        FromResult(await service.GetBoardAsync(request.BranchId, request.Date, ct));
}