using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Scaffold.Entities;

[Table("branches", Schema = "core")]
public partial class Branch
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("owner_id")]
    public Guid OwnerId { get; set; }

    [Column("name")]
    [StringLength(150)]
    public string Name { get; set; } = null!;

    [Column("address")]
    [StringLength(255)]
    public string Address { get; set; } = null!;

    [Column("latitude")]
    [Precision(9, 6)]
    public decimal? Latitude { get; set; }

    [Column("longitude")]
    [Precision(9, 6)]
    public decimal? Longitude { get; set; }

    [Column("phone_number")]
    [StringLength(20)]
    public string? PhoneNumber { get; set; }

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

    [Column("city")]
    [StringLength(100)]
    public string? City { get; set; }

    [Column("district")]
    [StringLength(100)]
    public string? District { get; set; }

    [Column("time_zone")]
    [StringLength(100)]
    public string TimeZone { get; set; } = null!;

    [Column("supports_instant_booking")]
    public bool SupportsInstantBooking { get; set; }

    [InverseProperty("Branch")]
    public virtual ICollection<BranchImage> BranchImages { get; set; } = new List<BranchImage>();

    [InverseProperty("Branch")]
    public virtual ICollection<BranchPricing> BranchPricings { get; set; } = new List<BranchPricing>();

    [InverseProperty("Branch")]
    public virtual ICollection<Court> Courts { get; set; } = new List<Court>();

    [ForeignKey("CreatedBy")]
    [InverseProperty("BranchCreatedByNavigations")]
    public virtual User? CreatedByNavigation { get; set; }

    [ForeignKey("DeletedBy")]
    [InverseProperty("BranchDeletedByNavigations")]
    public virtual User? DeletedByNavigation { get; set; }

    [InverseProperty("Branch")]
    public virtual OperatingHour? OperatingHour { get; set; }

    [ForeignKey("OwnerId")]
    [InverseProperty("Branches")]
    public virtual Owner Owner { get; set; } = null!;

    [InverseProperty("Branch")]
    public virtual ICollection<PlayerMatch> PlayerMatches { get; set; } = new List<PlayerMatch>();

    [ForeignKey("UpdatedBy")]
    [InverseProperty("BranchUpdatedByNavigations")]
    public virtual User? UpdatedByNavigation { get; set; }

    [ForeignKey("BranchId")]
    [InverseProperty("Branches")]
    public virtual ICollection<Amenity> Amenities { get; set; } = new List<Amenity>();
}
