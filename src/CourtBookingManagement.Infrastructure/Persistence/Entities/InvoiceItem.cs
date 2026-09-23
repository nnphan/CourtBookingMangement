using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Entities;

[Table("invoice_items", Schema = "payment")]
[Index("InvoiceId", Name = "ix_invoice_items_invoice")]
public partial class InvoiceItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("invoice_id")]
    public Guid InvoiceId { get; set; }

    [Column("description")]
    [StringLength(255)]
    public string Description { get; set; } = null!;

    [Column("quantity")]
    [Precision(10, 2)]
    public decimal Quantity { get; set; }

    [Column("unit_price")]
    [Precision(10, 2)]
    public decimal UnitPrice { get; set; }

    [Column("line_total")]
    [Precision(12, 2)]
    public decimal LineTotal { get; set; }

    [ForeignKey("InvoiceId")]
    [InverseProperty("InvoiceItems")]
    public virtual Invoice Invoice { get; set; } = null!;
}
