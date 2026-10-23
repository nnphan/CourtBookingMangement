using System.ComponentModel.DataAnnotations;

namespace CourtBookingManagement.Application.Users.DTOs;

public sealed class UserListRequestDto
{
    [StringLength(150)]
    public string? Keyword { get; set; }

    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 12;
}
