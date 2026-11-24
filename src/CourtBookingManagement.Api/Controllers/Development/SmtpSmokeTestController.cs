using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;

namespace CourtBookingManagement.Api.Controllers.Development;

/// <summary>Provides a development-only SMTP delivery check.</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[ApiExplorerSettings(IgnoreApi = true)]
[EnableRateLimiting("dev-smtp-smoke")]
[Route("api/dev/smtp-smoke-test")]
public sealed class SmtpSmokeTestController(
    IEmailSender emailSender,
    IHostEnvironment environment) : ApiControllerBase
{
    /// <summary>Sends a non-sensitive test message to the specified approved recipient.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Send(
        [FromBody] SmtpSmokeTestRequest request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var sent = await emailSender.SendAsync(
            new EmailMessage(
                request.Recipient,
                "SMTP development smoke test",
                "SMTP delivery is configured and working.",
                "<p>SMTP delivery is configured and working.</p>"),
            cancellationToken);

        return sent
            ? Success("SMTP test message sent.")
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiErrorResponse(
                false,
                "SMTP_DELIVERY_FAILED",
                "The SMTP test message could not be delivered.",
                HttpContext.TraceIdentifier,
                DateTimeOffset.UtcNow));
    }
}
