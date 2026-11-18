using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Payments.Contracts;
using CourtBookingManagement.Application.Payments.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Controllers;

[ApiController]
[Route("api/payments")]
[AllowAnonymous]
public sealed class PaymentsController(
    IPaymentService paymentService,
    ISePayPaymentService sePayPaymentService) : ApiControllerBase
{
    [HttpPost("sepay")]
    [ProducesResponseType(typeof(ApiResponse<CreateSePayPaymentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateSePayPayment(
        [FromBody] CreateSePayPaymentRequest request,
        [FromHeader(Name = "X-Booking-Hold-Token")] string? holdToken,
        CancellationToken cancellationToken) =>
        FromResult(
            await sePayPaymentService.CreatePaymentAsync(request, holdToken, cancellationToken),
            "SePay payment initialized successfully.");

    [HttpPost("momo")]
    [ProducesResponseType(typeof(ApiResponse<CreateMomoPaymentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateMomoPayment(
        [FromBody] CreateMomoPaymentRequest request,
        [FromHeader(Name = "X-Booking-Hold-Token")] string? holdToken,
        CancellationToken cancellationToken) =>
        FromResult(
            await paymentService.CreateMomoPaymentAsync(request, holdToken, cancellationToken),
            "MoMo payment initialized successfully.");

    [HttpGet("{paymentId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GetPaymentStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayment(
        Guid paymentId,
        [FromHeader(Name = "X-Booking-Hold-Token")] string? holdToken,
        CancellationToken cancellationToken) =>
        FromResult(
            await paymentService.GetPaymentStatusAsync(paymentId, holdToken, cancellationToken),
            "Payment status retrieved successfully.");

    [HttpPost("momo/{paymentId:guid}/query")]
    [ProducesResponseType(typeof(ApiResponse<GetPaymentStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> QueryMomoPayment(
        Guid paymentId,
        [FromHeader(Name = "X-Booking-Hold-Token")] string? holdToken,
        CancellationToken cancellationToken) =>
        FromResult(
            await paymentService.QueryMomoPaymentAsync(paymentId, holdToken, cancellationToken),
            "MoMo payment status queried successfully.");

    [HttpGet("/api/bookings/{bookingId:guid}/payment-status")]
    [ProducesResponseType(typeof(ApiResponse<GetPaymentStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBookingPaymentStatus(
        Guid bookingId,
        [FromHeader(Name = "X-Booking-Hold-Token")] string? holdToken,
        CancellationToken cancellationToken) =>
        FromResult(
            await paymentService.GetBookingPaymentStatusAsync(bookingId, holdToken, cancellationToken),
            "Booking payment status retrieved successfully.");
}
