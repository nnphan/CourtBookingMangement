using System.ComponentModel.DataAnnotations;

namespace CourtBookingManagement.Application.StaffManagement.DTOs;

/// <summary>Filters and pages Staff visible to the authenticated Branch Owner.</summary>
public sealed record StaffListRequest
{
    /// <summary>Restricts results to one active branch owned by the caller.</summary>
    public Guid? BranchId { get; init; }

    /// <summary>Searches display name, email, phone number, and Staff code.</summary>
    [StringLength(100)]
    public string? Search { get; init; }

    /// <summary>Filters by account status: ACTIVE or INACTIVE.</summary>
    [StringLength(16)]
    public string? Status { get; init; }

    /// <summary>Filters by activation status.</summary>
    [StringLength(32)]
    public string? ActivationStatus { get; init; }

    /// <summary>Whitelisted sort field; defaults to createdAt.</summary>
    [StringLength(32)]
    public string SortBy { get; init; } = "createdAt";

    /// <summary>ASC or DESC; defaults to DESC.</summary>
    [StringLength(4)]
    public string SortDirection { get; init; } = "DESC";

    /// <summary>One-based page number.</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    /// <summary>Page size from 10 through 100.</summary>
    [Range(10, 100)]
    public int PageSize { get; init; } = 20;
}
