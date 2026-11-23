namespace CourtBookingManagement.Application.StaffManagement.Models;

public sealed record StaffOwnerScope(
    Guid OwnerId,
    IReadOnlyList<StaffOwnerBranch> Branches);

public sealed record StaffOwnerBranch(Guid BranchId, string BranchName);
