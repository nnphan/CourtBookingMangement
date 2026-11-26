using System.ComponentModel.DataAnnotations;

namespace CourtBookingManagement.Application.Users.DTOs;

public sealed class ProvisionUserRequest
{
    [Required, EmailAddress, StringLength(100)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(150)]
    public string FullName { get; init; } = string.Empty;

    [Required, StringLength(72, MinimumLength = 8)]
    public string Password { get; init; } = string.Empty;

    [StringLength(20)]
    public string? PhoneNumber { get; init; }

    public bool IsActive { get; init; } = true;

    [Required]
    public IReadOnlyList<Guid> RoleIds { get; init; } = [];
}
