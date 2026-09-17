using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Entities;

[Table("courts", Schema = "core")]
[Index("BranchId", "CourtNumber", Name = "ux_courts_branch_number", IsUnique = true)]
public partial class Court
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("branch_id")]
    public Guid BranchId { get; set; }

    [Column("court_type_id")]
    public Guid? CourtTypeId { get; set; }

    [Column("court_number")]
    [StringLength(20)]
    public string CourtNumber { get; set; } = null!;

    [Column("name")]
    [StringLength(100)]
    public string? Name { get; set; }

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Column("is_active")]
    public bool IsActive { get; set; }

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
    [InverseProperty("Courts")]
    public virtual Branch Branch { get; set; } = null!;

    [InverseProperty("Court")]
    public virtual ICollection<CourtImage> CourtImages { get; set; } = new List<CourtImage>();

    [ForeignKey("CourtTypeId")]
    [InverseProperty("Courts")]
    public virtual CourtType? CourtType { get; set; }

    [ForeignKey("CreatedBy")]
    [InverseProperty("CourtCreatedByNavigations")]
    public virtual User? CreatedByNavigation { get; set; }

    [ForeignKey("DeletedBy")]
    [InverseProperty("CourtDeletedByNavigations")]
    public virtual User? DeletedByNavigation { get; set; }

    [ForeignKey("UpdatedBy")]
    [InverseProperty("CourtUpdatedByNavigations")]
    public virtual User? UpdatedByNavigation { get; set; }
}
