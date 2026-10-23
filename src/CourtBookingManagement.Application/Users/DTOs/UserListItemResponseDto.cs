namespace CourtBookingManagement.Application.Users.DTOs;

public sealed class UserListItemResponseDto
{
    public Guid Id { get; init; }

    public string? FullName { get; init; }

    public required string Email { get; init; }

    public string? PhoneNumber { get; init; }

    public bool IsActive { get; init; }

    public IReadOnlyCollection<UserListRoleResponseDto> Roles { get; init; } = [];
}
