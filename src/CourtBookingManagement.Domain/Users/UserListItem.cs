namespace CourtBookingManagement.Domain.Users;

public sealed record UserListItem(
    Guid Id,
    string? FullName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    IReadOnlyList<UserListRole> Roles);

public sealed record UserListRole(
    Guid Id,
    string Code,
    string Name);
