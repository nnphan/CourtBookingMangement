using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Entities;

[Table("refunds", Schema = "payment")]
[Index("PaymentId", Name = "ix_refunds_payment")]
public partial class Refund
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("payment_id")]
    public Guid PaymentId { get; set; }

    [Column("amount")]
    [Precision(12, 2)]
    public decimal Amount { get; set; }

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Column("reason")]
    public string? Reason { get; set; }

    [Column("requested_at")]
    public DateTime RequestedAt { get; set; }

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [Column("processed_by")]
    public Guid? ProcessedBy { get; set; }

    [ForeignKey("ProcessedBy")]
    [InverseProperty("Refunds")]
    public virtual User? ProcessedByNavigation { get; set; }


}
