using System;
using System.Collections.Generic;
using CourtBookingManagement.Infrastructure.Persistence.Scaffold.Entities;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence.Scaffold;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Amenity> Amenities { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<BranchImage> BranchImages { get; set; }

    public virtual DbSet<BranchPricing> BranchPricings { get; set; }

    public virtual DbSet<Court> Courts { get; set; }

    public virtual DbSet<CourtType> CourtTypes { get; set; }

    public virtual DbSet<Customer> Customers { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<InvoiceItem> InvoiceItems { get; set; }

    public virtual DbSet<MatchJoinRequest> MatchJoinRequests { get; set; }

    public virtual DbSet<MatchNotification> MatchNotifications { get; set; }

    public virtual DbSet<MatchParticipant> MatchParticipants { get; set; }

    public virtual DbSet<MembershipLevel> MembershipLevels { get; set; }

    public virtual DbSet<OperatingHour> OperatingHours { get; set; }

    public virtual DbSet<Owner> Owners { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PaymentMethod> PaymentMethods { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<PlayerMatch> PlayerMatches { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Refund> Refunds { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    public virtual DbSet<UserSession> UserSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresExtension("pg_trgm")
            .HasPostgresExtension("pgcrypto");

        modelBuilder.Entity<Amenity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("amenities_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("branches_pkey");

            entity.HasIndex(e => e.Name, "ix_branches_name_trgm")
                .HasMethod("gin")
                .HasOperators(new[] { "gin_trgm_ops" });

            entity.HasIndex(e => e.OwnerId, "ix_branches_owner").HasFilter("(deleted_at IS NULL)");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.SupportsInstantBooking).HasDefaultValue(false);
            entity.Property(e => e.TimeZone).HasDefaultValueSql("'Asia/Ho_Chi_Minh'::character varying");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.BranchCreatedByNavigations).HasConstraintName("branches_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.BranchDeletedByNavigations).HasConstraintName("branches_deleted_by_fkey");

            entity.HasOne(d => d.Owner).WithMany(p => p.Branches)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branches_owner_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.BranchUpdatedByNavigations).HasConstraintName("branches_updated_by_fkey");

            entity.HasMany(d => d.Amenities).WithMany(p => p.Branches)
                .UsingEntity<Dictionary<string, object>>(
                    "BranchesAmenity",
                    r => r.HasOne<Amenity>().WithMany()
                        .HasForeignKey("AmenityId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("branches_amenities_amenity_id_fkey"),
                    l => l.HasOne<Branch>().WithMany()
                        .HasForeignKey("BranchId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("branches_amenities_branch_id_fkey"),
                    j =>
                    {
                        j.HasKey("BranchId", "AmenityId").HasName("branches_amenities_pkey");
                        j.ToTable("branches_amenities", "core");
                        j.IndexerProperty<Guid>("BranchId").HasColumnName("branch_id");
                        j.IndexerProperty<Guid>("AmenityId").HasColumnName("amenity_id");
                    });
        });

        modelBuilder.Entity<BranchImage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("branch_images_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.SortOrder).HasDefaultValue((short)0);

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchImages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_images_branch_id_fkey");
        });

        modelBuilder.Entity<BranchPricing>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("branch_pricings_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Branch).WithMany(p => p.BranchPricings)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branch_pricings_branch_id_fkey");
        });

        modelBuilder.Entity<Court>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("courts_pkey");

            entity.HasIndex(e => new { e.BranchId, e.Status }, "ix_courts_branch_status").HasFilter("(deleted_at IS NULL)");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Status).HasDefaultValueSql("'active'::character varying");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Branch).WithMany(p => p.Courts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("courts_branch_id_fkey");

            entity.HasOne(d => d.CourtType).WithMany(p => p.Courts).HasConstraintName("courts_court_type_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CourtCreatedByNavigations).HasConstraintName("courts_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.CourtDeletedByNavigations).HasConstraintName("courts_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CourtUpdatedByNavigations).HasConstraintName("courts_updated_by_fkey");
        });

        modelBuilder.Entity<CourtType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("court_types_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("customers_pkey");

            entity.HasIndex(e => e.FullName, "ix_customers_name_trgm")
                .HasMethod("gin")
                .HasOperators(new[] { "gin_trgm_ops" });

            entity.HasIndex(e => e.UserId, "ux_customers_user")
                .IsUnique()
                .HasFilter("((deleted_at IS NULL) AND (user_id IS NOT NULL))");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsGuest).HasDefaultValue(false);
            entity.Property(e => e.LoyaltyPointsBalance).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.CustomerCreatedByNavigations).HasConstraintName("customers_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.CustomerDeletedByNavigations).HasConstraintName("customers_deleted_by_fkey");

            entity.HasOne(d => d.MembershipLevel).WithMany(p => p.Customers).HasConstraintName("customers_membership_level_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.CustomerUpdatedByNavigations).HasConstraintName("customers_updated_by_fkey");

            entity.HasOne(d => d.User).WithOne(p => p.CustomerUser).HasConstraintName("customers_user_id_fkey");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invoices_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.IssuedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("invoice_items_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.Quantity).HasDefaultValueSql("1");

            entity.HasOne(d => d.Invoice).WithMany(p => p.InvoiceItems).HasConstraintName("invoice_items_invoice_id_fkey");
        });

        modelBuilder.Entity<MatchJoinRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("match_join_requests_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Status).HasDefaultValueSql("'PENDING'::character varying");

            entity.HasOne(d => d.Match).WithMany(p => p.MatchJoinRequests)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("match_join_requests_match_id_fkey");

            entity.HasOne(d => d.Requester).WithMany(p => p.MatchJoinRequestRequesters)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("match_join_requests_requester_id_fkey");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.MatchJoinRequestReviewedByNavigations).HasConstraintName("match_join_requests_reviewed_by_fkey");
        });

        modelBuilder.Entity<MatchNotification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("match_notifications_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsRead).HasDefaultValue(false);

            entity.HasOne(d => d.Match).WithMany(p => p.MatchNotifications).HasConstraintName("match_notifications_match_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.MatchNotifications)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("match_notifications_user_id_fkey");
        });

        modelBuilder.Entity<MatchParticipant>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("match_participants_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.JoinedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Role).HasDefaultValueSql("'PLAYER'::character varying");
            entity.Property(e => e.Status).HasDefaultValueSql("'ACTIVE'::character varying");

            entity.HasOne(d => d.Match).WithMany(p => p.MatchParticipants)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("match_participants_match_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.MatchParticipants)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("match_participants_user_id_fkey");
        });

        modelBuilder.Entity<MembershipLevel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("membership_levels_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.MinPoints).HasDefaultValue(0);
            entity.Property(e => e.SortOrder).HasDefaultValue((short)0);
        });

        modelBuilder.Entity<OperatingHour>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("operating_hours_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.IsClosed).HasDefaultValue(false);

            entity.HasOne(d => d.Branch).WithOne(p => p.OperatingHour)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("operating_hours_branch_id_fkey");
        });

        modelBuilder.Entity<Owner>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("owners_pkey");

            entity.HasIndex(e => e.UserId, "ux_owners_user")
                .IsUnique()
                .HasFilter("(deleted_at IS NULL)");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.OwnerCreatedByNavigations).HasConstraintName("owners_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.OwnerDeletedByNavigations).HasConstraintName("owners_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.OwnerUpdatedByNavigations).HasConstraintName("owners_updated_by_fkey");

            entity.HasOne(d => d.User).WithOne(p => p.OwnerUser)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("owners_user_id_fkey");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("payments_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Status).HasDefaultValueSql("'pending'::character varying");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Payments).HasConstraintName("payments_created_by_fkey");

            entity.HasOne(d => d.PaymentMethod).WithMany(p => p.Payments)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("payments_payment_method_id_fkey");
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("payment_methods_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("permissions_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<PlayerMatch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("player_matches_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CurrentPlayers).HasDefaultValue(1);
            entity.Property(e => e.Status).HasDefaultValueSql("'OPEN'::character varying");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Branch).WithMany(p => p.PlayerMatches)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("player_matches_branch_id_fkey");

            entity.HasOne(d => d.Court).WithMany(p => p.PlayerMatches).HasConstraintName("player_matches_court_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.PlayerMatches)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("player_matches_created_by_fkey");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("refresh_tokens_pkey");

            entity.HasIndex(e => e.UserId, "ix_refresh_tokens_user").HasFilter("(revoked_at IS NULL)");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.IssuedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.ReplacedByTokenNavigation).WithMany(p => p.InverseReplacedByTokenNavigation).HasConstraintName("refresh_tokens_replaced_by_token_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens).HasConstraintName("refresh_tokens_user_id_fkey");
        });

        modelBuilder.Entity<Refund>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("refunds_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Status).HasDefaultValueSql("'pending'::character varying");

            entity.HasOne(d => d.ProcessedByNavigation).WithMany(p => p.Refunds).HasConstraintName("refunds_processed_by_fkey");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pkey");

            entity.HasIndex(e => e.Code, "ux_roles_code")
                .IsUnique()
                .HasFilter("(deleted_at IS NULL)");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.RoleCreatedByNavigations).HasConstraintName("roles_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.RoleDeletedByNavigations).HasConstraintName("roles_deleted_by_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.RoleUpdatedByNavigations).HasConstraintName("roles_updated_by_fkey");

            entity.HasMany(d => d.Permissions).WithMany(p => p.Roles)
                .UsingEntity<Dictionary<string, object>>(
                    "RolePermission",
                    r => r.HasOne<Permission>().WithMany()
                        .HasForeignKey("PermissionId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("role_permissions_permission_id_fkey"),
                    l => l.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("role_permissions_role_id_fkey"),
                    j =>
                    {
                        j.HasKey("RoleId", "PermissionId").HasName("pk_role_permissions");
                        j.ToTable("role_permissions", "auth");
                        j.IndexerProperty<Guid>("RoleId").HasColumnName("role_id");
                        j.IndexerProperty<Guid>("PermissionId").HasColumnName("permission_id");
                    });
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("transactions_pkey");

            entity.HasIndex(e => e.GatewayReference, "ux_transactions_gateway_ref")
                .IsUnique()
                .HasFilter("(gateway_reference IS NOT NULL)");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Status).HasDefaultValueSql("'pending'::character varying");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.HasIndex(e => e.Email, "ux_users_email")
                .IsUnique()
                .HasFilter("(deleted_at IS NULL)");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsEmailVerified).HasDefaultValue(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId }).HasName("user_roles_pkey");

            entity.Property(e => e.AssignedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.AssignedByNavigation).WithMany(p => p.UserRoleAssignedByNavigations).HasConstraintName("user_roles_assigned_by_fkey");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("user_roles_role_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoleUsers).HasConstraintName("user_roles_user_id_fkey");
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_sessions_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.LastSeenAt).HasDefaultValueSql("now()");
            entity.Property(e => e.StartedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.User).WithMany(p => p.UserSessions).HasConstraintName("user_sessions_user_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
