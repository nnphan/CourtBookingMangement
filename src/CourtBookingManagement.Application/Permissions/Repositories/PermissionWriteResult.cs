using CourtBookingManagement.Application.Permissions.Models.Responses;

namespace CourtBookingManagement.Application.Permissions.Repositories;

public enum PermissionWriteStatus
{
    Success,
    NotFound,
    CodeAlreadyExists
}

public sealed record PermissionWriteResult(
    PermissionWriteStatus Status,
    PermissionResponse? Permission = null);

public enum PermissionDeleteStatus
{
    Deleted,
    NotFound,
    InUse
}
