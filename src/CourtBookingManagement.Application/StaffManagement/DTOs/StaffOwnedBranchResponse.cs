namespace CourtBookingManagement.Application.StaffManagement.DTOs;

/// <summary>Represents a branch available to the Staff management owner.</summary>
public sealed record StaffOwnedBranchResponse(Guid BranchId, string BranchName, bool CanCreateStaff);
