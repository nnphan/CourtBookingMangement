using CourtBookingManagement.Application.Payments.Contracts;
using CourtBookingManagement.Application.Payments.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/payments/momo")]
[AllowAnonymous]
public sealed class MomoCallbacksController(IMomoIpnService ipnService) : ControllerBase
{
    [HttpPost("ipn")]
    [ProducesResponseType(typeof(MomoIpnResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> IpN(
        [FromBody] MomoIpnRequest request,
        CancellationToken cancellationToken)
    {
        var result = await ipnService.ProcessAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("return")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Return() =>
        Ok(new
        {
            message = "Payment return received. Verify the final result with the payment status endpoint."
        });
}
