using CourtBookingManagement.Application.Roles.Models.Responses;

namespace CourtBookingManagement.Application.Roles.Repositories;

public enum RoleWriteStatus
{
    Success,
    NotFound,
    CodeAlreadyExists,
    PermissionsNotFound,
    InUse
}

public sealed record RoleWriteResult(
    RoleWriteStatus Status,
    RoleResponse? Role = null);

public enum RoleDeleteStatus
{
    Deleted,
    NotFound,
    InUse
}
