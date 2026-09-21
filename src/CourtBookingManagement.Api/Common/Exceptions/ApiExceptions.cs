namespace CourtBookingManagement.Api.Common.Exceptions;

public abstract class ApiException(string message, string errorCode) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class ValidationException(
    IReadOnlyCollection<Responses.ValidationError> errors,
    string message = "One or more validation errors occurred.")
    : ApiException(message, Constants.ErrorCodes.Validation)
{
    public IReadOnlyCollection<Responses.ValidationError> Errors { get; } = errors;
}

public sealed class BadRequestException(string message, string errorCode = Constants.ErrorCodes.BadRequest)
    : ApiException(message, errorCode);

public sealed class NotFoundException(string message, string errorCode = Constants.ErrorCodes.NotFound)
    : ApiException(message, errorCode);

public sealed class ConflictException(string message, string errorCode = Constants.ErrorCodes.Conflict)
    : ApiException(message, errorCode);

public sealed class BusinessException(string message, string errorCode)
    : ApiException(message, errorCode);

public sealed class UnauthorizedException(string message = "Authentication is required.")
    : ApiException(message, Constants.ErrorCodes.Unauthorized);

public sealed class ForbiddenException(string message = "You do not have permission to perform this operation.")
    : ApiException(message, Constants.ErrorCodes.Forbidden);