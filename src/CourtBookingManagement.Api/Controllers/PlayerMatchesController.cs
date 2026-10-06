using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Services;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/player-matches")]
public sealed class PlayerMatchesController(IPlayerMatchService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] PlayerMatchSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await service.GetMatchesAsync(request, cancellationToken);
        return result.IsSuccess
            ? Success(result.Value.Items, "Success", result.Value.Metadata)
            : Error(result.Error);
    }
}
