using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Entities;

[Table("customers", Schema = "customer")]
[Index("PhoneNumber", Name = "ix_customers_phone")]
public partial class Customer
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } 

    [Column("user_id")]
    public Guid? UserId { get; set; }

    [Column("membership_level_id")]
    public Guid? MembershipLevelId { get; set; }

    [Column("full_name")]
    [StringLength(150)]
    public string FullName { get; set; } = null!;

    [Column("email")]
    [StringLength(20)]
    public string? Email { get; set; }

    [Column("phone_number")]
    [StringLength(20)]
    public string PhoneNumber { get; set; } = null!;

    [Column("is_guest")]
    public bool IsGuest { get; set; }

    [Column("loyalty_points_balance")]
    public int LoyaltyPointsBalance { get; set; }

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

    [ForeignKey("CreatedBy")]
    [InverseProperty("CustomerCreatedByNavigations")]
    public virtual User? CreatedByNavigation { get; set; }

    [ForeignKey("DeletedBy")]
    [InverseProperty("CustomerDeletedByNavigations")]
    public virtual User? DeletedByNavigation { get; set; }

    [ForeignKey("MembershipLevelId")]
    [InverseProperty("Customers")]
    public virtual MembershipLevel? MembershipLevel { get; set; }

    [ForeignKey("UpdatedBy")]
    [InverseProperty("CustomerUpdatedByNavigations")]
    public virtual User? UpdatedByNavigation { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("CustomerUser")]
    public virtual User? User { get; set; }
}
