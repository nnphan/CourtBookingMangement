using System.ComponentModel.DataAnnotations;

namespace CourtBookingManagement.Application.UserRoles.Models.Requests;

/// <summary>Replaces a user's global roles; an empty list removes all global roles.</summary>
public sealed record UpdateUserRolesRequest
{
    [Required]
    public IReadOnlyList<Guid>? RoleIds { get; init; }
}
