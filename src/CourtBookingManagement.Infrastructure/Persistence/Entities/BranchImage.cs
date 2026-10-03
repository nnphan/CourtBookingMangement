using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Entities;

[Table("branch_images", Schema = "core")]
[Index("BranchId", Name = "ix_branch_images_branch")]
public partial class BranchImage
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Column("image_url")]
    public string ImageUrl { get; set; } = null!;

    [Column("sort_order")]
    public short SortOrder { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("BranchImages")]
    public virtual Branch Branch { get; set; } = null!;
}
