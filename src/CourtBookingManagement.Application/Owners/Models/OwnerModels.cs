namespace CourtBookingManagement.Application.Owners.Models;

public sealed class OwnerSearchRequest
{
    public string? Keyword { get; set; }

    public bool? IsActive { get; set; }

    public string SortBy { get; set; } = "latest";

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 12;
}

public sealed class CreateOwnerRequest
{
    public Guid UserId { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public string? TaxId { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class UpdateOwnerRequest
{
    public string BusinessName { get; set; } = string.Empty;

    public string? TaxId { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class OwnerResponse
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string? FullName { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public string? TaxId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
