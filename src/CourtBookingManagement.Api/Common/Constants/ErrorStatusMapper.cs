using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Api.Common.Constants;

public static class ErrorStatusMapper
{
    public static int ToStatusCode(string errorCode) => errorCode switch
    {
        "Auth.InvalidCredentials" or "Auth.RefreshTokenInvalid" or
        "Auth.RefreshTokenRevoked" or "Auth.RefreshTokenExpired" => StatusCodes.Status401Unauthorized,
        "Auth.UserDisabled" or "Auth.EmailNotVerified" or "Auth.Unauthorized" => StatusCodes.Status403Forbidden,
        "User.NotFound" or "Branch.NotFound" or "PLAYER_MATCH.NOT_FOUND" or "PLAYER_MATCH.REQUEST_NOT_FOUND" => StatusCodes.Status404NotFound,
        "PLAYER_MATCH.NOT_HOST" => StatusCodes.Status403Forbidden,
        "User.EmailAlreadyExists" or "PLAYER_MATCH.ALREADY_JOINED" or "PLAYER_MATCH.REQUEST_EXISTS" => StatusCodes.Status409Conflict,
        "PLAYER_MATCH.REQUEST_STATE_CHANGED" => StatusCodes.Status409Conflict,
        "Error.Validation" or "PLAYER_MATCH.NOT_OPEN" or "PLAYER_MATCH.MATCH_EXPIRED" or "PLAYER_MATCH.MATCH_FULL" or "PLAYER_MATCH.HOST_ALREADY_PARTICIPANT" or "PLAYER_MATCH.REQUEST_MISMATCH" or "PLAYER_MATCH.REQUEST_NOT_PENDING" => StatusCodes.Status400BadRequest,
        "CourtBooking.NotFound" => StatusCodes.Status404NotFound,
        "CourtBooking.Conflict" => StatusCodes.Status409Conflict,
        "CourtBooking.Blocked" => StatusCodes.Status400BadRequest,
        "CourtBooking.Invalid" => StatusCodes.Status400BadRequest,
        "Error.Exception" => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status400BadRequest
    };
}