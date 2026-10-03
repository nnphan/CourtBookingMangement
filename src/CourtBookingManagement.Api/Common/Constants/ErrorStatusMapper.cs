using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Api.Common.Constants;

public static class ErrorStatusMapper
{
    public static int ToStatusCode(string errorCode) => errorCode switch
    {
        "Auth.InvalidCredentials" or "Auth.RefreshTokenInvalid" or
        "Auth.RefreshTokenRevoked" or "Auth.RefreshTokenExpired" => StatusCodes.Status401Unauthorized,
        "Auth.UserDisabled" or "Auth.EmailNotVerified" or "Auth.Unauthorized" => StatusCodes.Status403Forbidden,
        "User.NotFound" => StatusCodes.Status404NotFound,
        "User.EmailAlreadyExists" => StatusCodes.Status409Conflict,
        "Error.Validation" => StatusCodes.Status400BadRequest,
        "CourtBooking.NotFound" => StatusCodes.Status404NotFound,
        "CourtBooking.Conflict" => StatusCodes.Status409Conflict,
        "CourtBooking.Blocked" => StatusCodes.Status400BadRequest,
        "CourtBooking.Invalid" => StatusCodes.Status400BadRequest,
        "Error.Exception" => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status400BadRequest
    };
}