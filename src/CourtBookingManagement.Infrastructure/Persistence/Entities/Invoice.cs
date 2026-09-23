using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Entities;

[Table("invoices", Schema = "payment")]
[Index("BookingId", Name = "ux_invoices_booking", IsUnique = true)]
[Index("InvoiceNumber", Name = "ux_invoices_number", IsUnique = true)]
public partial class Invoice
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("booking_id")]
    public Guid BookingId { get; set; }

    [Column("invoice_number")]
    [StringLength(30)]
    public string InvoiceNumber { get; set; } = null!;

    [Column("issued_at")]
    public DateTime IssuedAt { get; set; }

    [Column("total_amount")]
    [Precision(12, 2)]
    public decimal TotalAmount { get; set; }

    [InverseProperty("Invoice")]
    public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
}
