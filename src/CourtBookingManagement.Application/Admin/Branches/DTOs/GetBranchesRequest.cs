using System.ComponentModel.DataAnnotations;

namespace CourtBookingManagement.Application.Admin.Branches.DTOs;

public sealed class GetBranchesRequest
{
    [StringLength(150)]
    public string? Keyword { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? District { get; set; }

    public bool? IsActive { get; set; }

    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;
}