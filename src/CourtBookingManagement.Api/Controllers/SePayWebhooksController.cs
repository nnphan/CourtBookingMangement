using CourtBookingManagement.Api.Common.Constants;
using CourtBookingManagement.Application.Payments.Contracts;
using CourtBookingManagement.Application.Payments.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/payments/sepay/webhook")]
[AllowAnonymous]
public sealed class SePayWebhooksController(ISePayWebhookService webhookService) : ControllerBase
{
    private const int MaximumBodyBytes = 65536;

    [HttpPost]
    [ProducesResponseType(typeof(SePayWebhookResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SePayWebhookResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(SePayWebhookResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(SePayWebhookResponse), StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        if (Request.ContentLength > MaximumBodyBytes)
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new SePayWebhookResponse(false));
        }

        await using var body = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var bytesRead = await Request.Body.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            if (body.Length + bytesRead > MaximumBodyBytes)
            {
                return StatusCode(
                    StatusCodes.Status413PayloadTooLarge,
                    new SePayWebhookResponse(false));
            }

            await body.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        var result = await webhookService.ProcessAsync(
            body.ToArray(),
            Request.Headers.Authorization.ToString(),
            Request.Headers["X-SePay-Signature"].ToString(),
            Request.Headers["X-SePay-Timestamp"].ToString(),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : StatusCode(
                ErrorStatusMapper.ToStatusCode(result.Error.Code),
                new SePayWebhookResponse(false));
    }
}
