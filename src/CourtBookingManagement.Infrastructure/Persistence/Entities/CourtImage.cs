using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Entities;

[Table("court_images", Schema = "core")]
[Index("CourtId", Name = "ix_court_images_court")]
public partial class CourtImage
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("court_id")]
    public Guid CourtId { get; set; }

    [Column("image_url")]
    public string ImageUrl { get; set; } = null!;

    [Column("sort_order")]
    public short SortOrder { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("CourtId")]
    [InverseProperty("CourtImages")]
    public virtual Court Court { get; set; } = null!;
}
