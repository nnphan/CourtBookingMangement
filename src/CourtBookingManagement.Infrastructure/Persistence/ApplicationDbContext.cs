using CourtBookingManagement.Application.Abstractions.Clock;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure.Persistence;

public partial class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IDateTimeProvider dateTimeProvider) : DbContext(options), IUnitOfWork
{
    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<Court> Courts { get; set; }

    public virtual DbSet<CourtImage> CourtImages { get; set; }

    public virtual DbSet<CourtType> CourtTypes { get; set; }

    public virtual DbSet<OperatingHour> OperatingHours { get; set; }

    public virtual DbSet<Owner> Owners { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    public virtual DbSet<UserSession> UserSessions { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext1).Assembly);
        base.OnModelCreating(modelBuilder);

        modelBuilder
            .HasPostgresExtension("pg_trgm")
            .HasPostgresExtension("pgcrypto");

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
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.BranchCreatedByNavigations).HasConstraintName("branches_created_by_fkey");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.BranchDeletedByNavigations).HasConstraintName("branches_deleted_by_fkey");

            entity.HasOne(d => d.Owner).WithMany(p => p.Branches)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("branches_owner_id_fkey");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.BranchUpdatedByNavigations).HasConstraintName("branches_updated_by_fkey");
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

        modelBuilder.Entity<CourtImage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("court_images_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.SortOrder).HasDefaultValue((short)0);

            entity.HasOne(d => d.Court).WithMany(p => p.CourtImages).HasConstraintName("court_images_court_id_fkey");
        });

        modelBuilder.Entity<CourtType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("court_types_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
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

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("permissions_pkey");

            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v7()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
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
                        .HasConstraintName("role_permissions_permission_id_fkey"),
                    l => l.HasOne<Role>().WithMany()
                        .HasForeignKey("RoleId")
                        .HasConstraintName("role_permissions_role_id_fkey"),
                    j =>
                    {
                        j.HasKey("RoleId", "PermissionId").HasName("role_permissions_pkey");
                        j.ToTable("role_permissions", "auth");
                        j.IndexerProperty<Guid>("RoleId").HasColumnName("role_id");
                        j.IndexerProperty<Guid>("PermissionId").HasColumnName("permission_id");
                    });
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

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        _ = dateTimeProvider.UtcNow;
        return await base.SaveChangesAsync(cancellationToken);
    }
}