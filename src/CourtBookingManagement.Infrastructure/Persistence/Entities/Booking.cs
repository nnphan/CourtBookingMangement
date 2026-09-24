using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Entities;

[PrimaryKey("Id", "CreatedAt")]
[Table("bookings", Schema = "booking")]
public partial class Booking
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("booking_code")]
    [StringLength(20)]
    public string BookingCode { get; set; } = null!;

    [Column("customer_id")]
    public Guid CustomerId { get; set; }

    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Column("booking_type")]
    [StringLength(20)]
    public string BookingType { get; set; } = null!;

    [Column("subtotal_amount")]
    [Precision(12, 2)]
    public decimal SubtotalAmount { get; set; }

    [Column("discount_amount")]
    [Precision(12, 2)]
    public decimal DiscountAmount { get; set; }

    [Column("total_amount")]
    [Precision(12, 2)]
    public decimal TotalAmount { get; set; }

    [Column("recurrence_rule")]
    public string? RecurrenceRule { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Key]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public Guid? CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Column("updated_by")]
    public Guid? UpdatedBy { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("deleted_by")]
    public Guid? DeletedBy { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("Bookings")]
    public virtual Branch Branch { get; set; } = null!;

    [InverseProperty("Booking")]
    public virtual ICollection<BookingDetail> BookingDetails { get; set; } = new List<BookingDetail>();

    [ForeignKey("CreatedBy")]
    [InverseProperty("BookingCreatedByNavigations")]
    public virtual User? CreatedByNavigation { get; set; }

    [ForeignKey("CustomerId")]
    [InverseProperty("Bookings")]
    public virtual Customer Customer { get; set; } = null!;

    [ForeignKey("DeletedBy")]
    [InverseProperty("BookingDeletedByNavigations")]
    public virtual User? DeletedByNavigation { get; set; }

    [ForeignKey("UpdatedBy")]
    [InverseProperty("BookingUpdatedByNavigations")]
    public virtual User? UpdatedByNavigation { get; set; }
}
