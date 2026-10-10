namespace CourtBookingManagement.Application.Admin.Branches.DTOs;

public sealed record BranchSummaryResponse(
    int TotalBranches,
    int ActiveBranches,
    int InactiveBranches,
    int TotalCourts);