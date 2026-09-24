using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.CourtStatus.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/branches")]
public sealed class BranchesController(ICourtStatusService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => FromResult(await service.GetBranchesAsync(ct));
}