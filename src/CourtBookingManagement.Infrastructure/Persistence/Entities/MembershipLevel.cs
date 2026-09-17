using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Entities;

[Table("membership_levels", Schema = "customer")]
[Index("Code", Name = "ux_membership_levels_code", IsUnique = true)]
public partial class MembershipLevel
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("code")]
    [StringLength(20)]
    public string Code { get; set; } = null!;

    [Column("name")]
    [StringLength(50)]
    public string Name { get; set; } = null!;

    [Column("min_points")]
    public int MinPoints { get; set; }

    [Column("discount_pct")]
    [Precision(5, 2)]
    public decimal DiscountPct { get; set; }

    [Column("sort_order")]
    public short SortOrder { get; set; }

    [InverseProperty("MembershipLevel")]
    public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();
}
