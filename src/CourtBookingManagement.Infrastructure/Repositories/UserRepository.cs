using DomainUser = CourtBookingManagement.Domain.Users.User;
using EfUser = CourtBookingManagement.Infrastructure.Persistence.Entities.User;
using CourtBookingManagement.Application.Auth.Exceptions;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Domain.Users;
using CourtBookingManagement.Infrastructure.Persistence.Mappings;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using CourtBookingManagement.Infrastructure.Persistence;
using System.Data;
using Dapper;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public UserRepository(
        ApplicationDbContext dbContext,
        ISqlConnectionFactory sqlConnectionFactory)
    {
        _dbContext = dbContext;
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<DomainUser?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext
            .Set<EfUser>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.Id == id && user.DeletedAt == null,
                cancellationToken);

        return entity?.ToDomain();
    }

    public async Task<IReadOnlyList<DomainUser>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext
            .Set<EfUser>()
            .AsNoTracking()
            .Where(user => user.DeletedAt == null)
            .OrderByDescending(user => user.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(entity => entity.ToDomain()).ToList();
    }

    public async Task<UserListPage> GetListWithRolesAsync(
        string? keyword,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        const string userFilter = """
            u.deleted_at IS NULL
            AND (
                @Keyword IS NULL
                OR u.full_name ILIKE '%' || @Keyword || '%'
                OR u.email ILIKE '%' || @Keyword || '%'
                OR u.phone_number ILIKE '%' || @Keyword || '%'
            )
            """;

        var sql = $"""
            SELECT COUNT(*)::bigint
            FROM auth.users u
            WHERE {userFilter};

            WITH paged_users AS
            (
                SELECT
                    u.id,
                    u.full_name,
                    u.email,
                    u.phone_number,
                    u.is_active
                FROM auth.users u
                WHERE {userFilter}
                ORDER BY u.full_name NULLS LAST, u.id
                LIMIT @PageSize
                OFFSET @Offset
            )
            SELECT
                u.id AS Id,
                u.full_name AS FullName,
                u.email AS Email,
                u.phone_number AS PhoneNumber,
                u.is_active AS IsActive,
                r.id AS RoleId,
                r.code AS RoleCode,
                r.name AS RoleName
            FROM paged_users u
            LEFT JOIN auth.user_roles ur ON ur.user_id = u.id
            LEFT JOIN auth.roles r
                ON r.id = ur.role_id
               AND r.deleted_at IS NULL
            ORDER BY
                u.full_name NULLS LAST,
                u.id,
                r.code NULLS LAST,
                r.id;
            """;

        var offset = ((long)pageNumber - 1) * pageSize;
        using var connection = _sqlConnectionFactory.CreateConnection();
        using var results = await connection.QueryMultipleAsync(new CommandDefinition(
            sql,
            new
            {
                Keyword = keyword,
                PageSize = pageSize,
                Offset = offset
            },
            cancellationToken: cancellationToken));
        var totalCount = await results.ReadSingleAsync<long>();
        var rows = await results.ReadAsync<UserListRow>();

        var users = new List<UserListAccumulator>();
        var usersById = new Dictionary<Guid, UserListAccumulator>();

        foreach (var row in rows)
        {
            if (!usersById.TryGetValue(row.Id, out var user))
            {
                user = new UserListAccumulator(
                    row.Id,
                    row.FullName,
                    row.Email,
                    row.PhoneNumber,
                    row.IsActive);
                usersById.Add(row.Id, user);
                users.Add(user);
            }

            if (row.RoleId is Guid roleId)
            {
                if (row.RoleCode is null || row.RoleName is null)
                {
                    throw new InvalidOperationException(
                        $"Role '{roleId}' has missing required role details.");
                }

                user.AddRole(new UserListRole(roleId, row.RoleCode, row.RoleName));
            }
        }

        var items = users
            .Select(user => new UserListItem(
                user.Id,
                user.FullName,
                user.Email,
                user.PhoneNumber,
                user.IsActive,
                user.Roles))
            .ToArray();

        return new UserListPage(items, totalCount);
    }

    public Task<bool> ExistsByEmailAsync(
        string email,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        return _dbContext
            .Set<EfUser>()
            .AnyAsync(
                user => user.DeletedAt == null
                    && user.Email.ToLower() == email.ToLower()
                    && (excludingId == null || user.Id != excludingId),
                cancellationToken);
    }

    public Task<bool> ExistsByPhoneNumberAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        return _dbContext
            .Set<EfUser>()
            .AnyAsync(
                user => user.DeletedAt == null && user.PhoneNumber == phoneNumber,
                cancellationToken);
    }

    public async Task AddAsync(
        DomainUser user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await _dbContext
            .Set<EfUser>()
            .AddAsync(user.ToPersistence(), cancellationToken);
    }

    public async Task UpdateAsync(
        DomainUser user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var entity = await _dbContext
            .Set<EfUser>()
            .SingleOrDefaultAsync(entity => entity.Id == user.Id, cancellationToken)
            ?? throw new InvalidOperationException($"User '{user.Id}' was not found.");

        user.ApplyTo(entity);
    }

    public async Task<Guid> CreateUserAsync(
        DomainUser user,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(transaction);

        const string sql = """
            INSERT INTO auth.users
            (
                id,
                email,
                full_name,
                phone_number,
                password_hash,
                is_active,
                is_email_verified,
                created_at,
                created_by,
                updated_at,
                updated_by
            )
            VALUES
            (
                @Id,
                @Email,
                @FullName,
                @PhoneNumber,
                @PasswordHash,
                @IsActive,
                @IsEmailVerified,
                @CreatedAt,
                @CreatedBy,
                @UpdatedAt,
                @UpdatedBy
            )
            RETURNING id;
            """;

        try
        {
            return await transaction.Connection!.ExecuteScalarAsync<Guid>(new CommandDefinition(
                sql,
                new
                {
                    user.Id,
                    user.Email,
                    user.FullName,
                    user.PhoneNumber,
                    user.PasswordHash,
                    user.IsActive,
                    user.IsEmailVerified,
                    user.CreatedAt,
                    user.CreatedBy,
                    user.UpdatedAt,
                    user.UpdatedBy
                },
                transaction,
                cancellationToken: cancellationToken));
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation
            && exception.ConstraintName is "ux_users_email" or "ux_users_active_phone_number")
        {
            throw new RegistrationConflictException(exception.ConstraintName!, exception);
        }
    }

    private sealed class UserListRow
    {
        public Guid Id { get; init; }

        public string? FullName { get; init; }

        public string Email { get; init; } = string.Empty;

        public string? PhoneNumber { get; init; }

        public bool IsActive { get; init; }

        public Guid? RoleId { get; init; }

        public string? RoleCode { get; init; }

        public string? RoleName { get; init; }
    }

    private sealed class UserListAccumulator(
        Guid id,
        string? fullName,
        string email,
        string? phoneNumber,
        bool isActive)
    {
        private readonly HashSet<Guid> _roleIds = [];
        private readonly List<UserListRole> _roles = [];

        public Guid Id { get; } = id;

        public string? FullName { get; } = fullName;

        public string Email { get; } = email;

        public string? PhoneNumber { get; } = phoneNumber;

        public bool IsActive { get; } = isActive;

        public IReadOnlyList<UserListRole> Roles => _roles;

        public void AddRole(UserListRole role)
        {
            if (_roleIds.Add(role.Id))
            {
                _roles.Add(role);
            }
        }
    }
}