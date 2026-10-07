namespace CourtBookingManagement.Application.UserRoles.Models.Responses;

public sealed class RoleItemResponse
{
    public Guid RoleId { get; set; }

    public string RoleCode { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;
}
