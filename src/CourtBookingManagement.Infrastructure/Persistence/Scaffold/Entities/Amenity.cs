using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Scaffold.Entities;

[Table("amenities", Schema = "core")]
public partial class Amenity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("code")]
    [StringLength(30)]
    public string Code { get; set; } = null!;

    [Column("name")]
    [StringLength(100)]
    public string Name { get; set; } = null!;

    [Column("icon")]
    [StringLength(50)]
    public string? Icon { get; set; }

    [ForeignKey("AmenityId")]
    [InverseProperty("Amenities")]
    public virtual ICollection<Branch> Branches { get; set; } = new List<Branch>();
}
