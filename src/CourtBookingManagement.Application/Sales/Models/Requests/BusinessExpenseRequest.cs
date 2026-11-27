using System.ComponentModel.DataAnnotations;

namespace CourtBookingManagement.Application.Sales.Models.Requests;

public sealed record BusinessExpenseRequest(
    Guid BranchId,
    DateOnly Date,
    [Required, RegularExpression("electricity|water|payroll|rent|maintenance|cleaning|internet_software|marketing|supplies|other")]
    string Category,
    [Required, StringLength(200)] string Title,
    [Range(typeof(decimal), "0.01", "999999999999.99")] decimal Amount,
    [StringLength(1000)] string? Notes);
