using CourtBookingManagement.Api.Common.Constants;
using CourtBookingManagement.Api.Common.Exceptions;
using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Branches.Services;
using Microsoft.AspNetCore.Diagnostics;
using Npgsql;

namespace CourtBookingManagement.Api.Common.Middleware;

public sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, errorCode, message, validationErrors) = exception switch
        {
            BranchAlreadyExistsException => (
                StatusCodes.Status409Conflict,
                ErrorCodes.Conflict,
                exception.Message,
                (IReadOnlyCollection<ValidationError>?)null),
            InvalidBranchAmenitiesException => (
                StatusCodes.Status400BadRequest,
                ErrorCodes.BadRequest,
                exception.Message,
                (IReadOnlyCollection<ValidationError>?)null),
            BranchOwnerNotFoundException => (
                StatusCodes.Status403Forbidden,
                ErrorCodes.Forbidden,
                exception.Message,
                (IReadOnlyCollection<ValidationError>?)null),
            FluentValidation.ValidationException validationException => (
                StatusCodes.Status400BadRequest,
                ErrorCodes.Validation,
                validationException.Message,
                validationException.Errors
                    .Select(error => new ValidationError(error.PropertyName, error.ErrorMessage, error.ErrorCode))
                    .ToArray()),
            ApiException apiException => (
                apiException switch
                {
                    UnauthorizedException => StatusCodes.Status401Unauthorized,
                    ForbiddenException => StatusCodes.Status403Forbidden,
                    NotFoundException => StatusCodes.Status404NotFound,
                    ConflictException => StatusCodes.Status409Conflict,
                    ValidationException => StatusCodes.Status400BadRequest,
                    _ => StatusCodes.Status400BadRequest
                },
                apiException.ErrorCode,
                apiException.Message,
                (apiException as ValidationException)?.Errors),
            NpgsqlException databaseException => (
                databaseException.IsTransient
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status500InternalServerError,
                ErrorCodes.Database,
                "A database error occurred.",
                (IReadOnlyCollection<ValidationError>?)null),
            _ => (
                StatusCodes.Status500InternalServerError,
                ErrorCodes.Unexpected,
                environment.IsDevelopment() ? exception.Message : "An unexpected error occurred.",
                (IReadOnlyCollection<ValidationError>?)null)
        };

        if (statusCode >= 500)
        {
            logger.LogError(exception, "Unhandled API exception. TraceId: {TraceId}", httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogWarning(exception, "Handled API exception {ErrorCode}. TraceId: {TraceId}", errorCode, httpContext.TraceIdentifier);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new ApiErrorResponse(
                false,
                errorCode,
                message,
                httpContext.TraceIdentifier,
                DateTimeOffset.UtcNow,
                validationErrors),
            cancellationToken);

        return true;
    }
}