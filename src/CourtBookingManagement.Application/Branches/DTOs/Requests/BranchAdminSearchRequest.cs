using System.ComponentModel.DataAnnotations;

namespace CourtBookingManagement.Application.Branches.DTOs.Requests;

public sealed record BranchAdminSearchRequest
{
    [StringLength(150)]
    public string? Keyword { get; init; }

    [StringLength(100)]
    public string? City { get; init; }

    [StringLength(100)]
    public string? District { get; init; }

    public bool? IsActive { get; init; }

    [Range(1, int.MaxValue)]
    public int PageNumber { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 12;
}