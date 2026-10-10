namespace CourtBookingManagement.Application.Admin.Branches.DTOs;

public sealed record BranchListResponse(
    Guid Id,
    string Name,
    string? City,
    string? District,
    string? PhoneNumber,
    int TotalCourts,
    bool IsActive,
    DateTime CreatedAt);