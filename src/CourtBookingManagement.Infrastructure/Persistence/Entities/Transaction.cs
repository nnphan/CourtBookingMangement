using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Entities;

[Table("transactions", Schema = "payment")]
[Index("PaymentId", Name = "ix_transactions_payment")]
public partial class Transaction
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("payment_id")]
    public Guid PaymentId { get; set; }

    [Column("gateway_reference")]
    [StringLength(100)]
    public string? GatewayReference { get; set; }

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Column("raw_response", TypeName = "jsonb")]
    public string? RawResponse { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
