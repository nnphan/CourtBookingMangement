using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Entities;

[Table("operating_hours", Schema = "core")]
[Index("BranchId", Name = "ux_operating_hours", IsUnique = true)]
public partial class OperatingHour
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Column("open_time")]
    public TimeOnly OpenTime { get; set; }

    [Column("close_time")]
    public TimeOnly CloseTime { get; set; }

    [Column("is_closed")]
    public bool IsClosed { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("OperatingHour")]
    public virtual Branch Branch { get; set; } = null!;
}
