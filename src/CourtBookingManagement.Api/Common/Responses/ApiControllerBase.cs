using CourtBookingManagement.Api.Common.Constants;
using CourtBookingManagement.Domain.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace CourtBookingManagement.Api.Common.Responses;

public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult Success<T>(T data, string message = "Request completed successfully.", object? metadata = null) =>
        Ok(ApiResponse<T>.Create(data, message, HttpContext.TraceIdentifier, metadata));

    protected IActionResult Success(string message = "Request completed successfully.") =>
        Ok(ApiResponse<object?>.Create(null, message, HttpContext.TraceIdentifier));

    protected IActionResult Created<T>(T data, string actionName, object routeValues, string message = "Resource created successfully.") =>
        CreatedAtAction(actionName, routeValues, ApiResponse<T>.Create(data, message, HttpContext.TraceIdentifier));

    protected IActionResult FromResult<T>(Result<T> result, string message = "Request completed successfully.") =>
        result.IsSuccess
            ? Success(result.Value, message)
            : Error(result.Error);

    protected IActionResult FromResult(Result result, string message = "Request completed successfully.") =>
        result.IsSuccess ? Success(message) : Error(result.Error);

    protected IActionResult Error(Error error)
    {
        var statusCode = ErrorStatusMapper.ToStatusCode(error.Code);
        var response = new ApiErrorResponse(
            false,
            error.Code,
            error.Description,
            HttpContext.TraceIdentifier,
            DateTimeOffset.UtcNow);

        return StatusCode(statusCode, response);
    }
}