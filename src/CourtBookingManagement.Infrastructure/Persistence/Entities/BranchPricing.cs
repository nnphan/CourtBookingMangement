using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Entities;

[Table("branch_pricings", Schema = "core")]
public sealed class BranchPricing
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Column("pricing_type")]
    [StringLength(30)]
    public string PricingType { get; set; } = null!;

    [Column("start_time")]
    public TimeOnly StartTime { get; set; }

    [Column("end_time")]
    public TimeOnly EndTime { get; set; }

    [Column("price_per_hour")]
    [Precision(12, 2)]
    public decimal PricePerHour { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey(nameof(BranchId))]
    public Branch Branch { get; set; } = null!;
}