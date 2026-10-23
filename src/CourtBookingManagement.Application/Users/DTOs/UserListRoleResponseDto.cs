namespace CourtBookingManagement.Application.Users.DTOs;

public sealed class UserListRoleResponseDto
{
    public Guid Id { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }
}
