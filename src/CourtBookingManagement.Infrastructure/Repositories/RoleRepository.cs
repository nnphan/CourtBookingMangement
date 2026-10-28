using System.Data;
using System.Text;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.Roles.Models.Requests;
using CourtBookingManagement.Application.Roles.Models.Responses;
using CourtBookingManagement.Application.Roles.Repositories;
using Dapper;
using Npgsql;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class RoleRepository(ISqlConnectionFactory sqlConnectionFactory) : IRoleRepository
{
    public async Task<PagedResult<RoleResponse>> SearchAsync(
        RoleSearchRequest request,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        var dataParameters = CreateParameters(request);
        var dataSql = new StringBuilder();

        AppendBaseQuery(dataSql);
        dataSql.AppendLine("SELECT * FROM role_data WHERE 1 = 1");
        AppendFilters(dataSql, dataParameters, request);
        dataSql.AppendLine(GetSortExpression(request.SortBy));
        dataSql.AppendLine("LIMIT @PageSize OFFSET @Offset;");
        dataParameters.Add("PageSize", request.PageSize);
        dataParameters.Add("Offset", ((long)request.Page - 1) * request.PageSize);

        var items = (await connection.QueryAsync<RoleResponse>(
            new CommandDefinition(dataSql.ToString(), dataParameters, cancellationToken: cancellationToken))).AsList();
        await LoadPermissionsAsync(connection, items, cancellationToken);

        var countParameters = CreateParameters(request);
        var countSql = new StringBuilder();
        AppendBaseQuery(countSql);
        countSql.AppendLine("SELECT COUNT(*) FROM role_data WHERE 1 = 1");
        AppendFilters(countSql, countParameters, request);

        var totalCount = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(countSql.ToString(), countParameters, cancellationToken: cancellationToken));

        return PagedResult<RoleResponse>.Create(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<RoleResponse?> GetByIdWithPermissionsAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                r.id AS Id,
                r.code AS Code,
                r.name AS Name,
                r.description AS Description,
                r.is_active AS IsActive,
                (SELECT COUNT(DISTINCT ur.user_id)::int FROM auth.user_roles ur WHERE ur.role_id = r.id) AS UserCount,
                (SELECT COUNT(DISTINCT rp.permission_id)::int FROM auth.role_permissions rp WHERE rp.role_id = r.id) AS PermissionCount,
                r.created_at AS CreatedAt,
                r.updated_at AS UpdatedAt
            FROM auth.roles r
            WHERE r.id = @Id
              AND r.deleted_at IS NULL;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        var role = await connection.QuerySingleOrDefaultAsync<RoleResponse>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        if (role is null)
        {
            return null;
        }

        await LoadPermissionsAsync(connection, [role], cancellationToken);
        return role;
    }

    public async Task<bool> ExistsByCodeAsync(
        string code,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM auth.roles
                WHERE code = @Code
                  AND deleted_at IS NULL
                  AND (@ExcludeId IS NULL OR id <> @ExcludeId)
            );
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { Code = code, ExcludeId = excludeId }, cancellationToken: cancellationToken));
    }

    public async Task<RoleWriteResult> CreateAsync(
        CreateRoleRequest request,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var validPermissionIds = await GetValidPermissionIdsAsync(
            connection,
            transaction,
            request.PermissionIds,
            cancellationToken);
        if (validPermissionIds.Count != request.PermissionIds.Count)
        {
            return new RoleWriteResult(RoleWriteStatus.PermissionsNotFound);
        }

        try
        {
            var roleId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
                """
                INSERT INTO auth.roles
                    (code, name, description, is_active, created_at, created_by, updated_at, updated_by)
                VALUES
                    (@Code, @Name, @Description, @IsActive, NOW(), @ActorId, NOW(), @ActorId)
                RETURNING id;
                """,
                new
                {
                    request.Code,
                    request.Name,
                    request.Description,
                    request.IsActive,
                    ActorId = actorId
                },
                transaction,
                cancellationToken: cancellationToken));

            if (request.PermissionIds.Count > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO auth.role_permissions (role_id, permission_id)
                    SELECT @RoleId, permission_id
                    FROM UNNEST(@PermissionIds) AS permission_id
                    ON CONFLICT (role_id, permission_id) DO NOTHING;
                    """,
                    new { RoleId = roleId, PermissionIds = request.PermissionIds.ToArray() },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            var role = await GetByIdWithPermissionsAsync(connection, transaction, roleId, cancellationToken);
            transaction.Commit();
            return new RoleWriteResult(RoleWriteStatus.Success, role);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            transaction.Rollback();
            return new RoleWriteResult(RoleWriteStatus.CodeAlreadyExists);
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.ForeignKeyViolation &&
            string.Equals(exception.ConstraintName, "role_permissions_permission_id_fkey", StringComparison.Ordinal))
        {
            transaction.Rollback();
            return new RoleWriteResult(RoleWriteStatus.PermissionsNotFound);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<RoleWriteResult> UpdateAsync(
        Guid id,
        UpdateRoleRequest request,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var exists = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                "SELECT id FROM auth.roles WHERE id = @Id AND deleted_at IS NULL FOR UPDATE;",
                new { Id = id },
                transaction,
                cancellationToken: cancellationToken));
        if (exists is null)
        {
            return new RoleWriteResult(RoleWriteStatus.NotFound);
        }

        var codeExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1 FROM auth.roles
                WHERE code = @Code
                  AND deleted_at IS NULL
                  AND id <> @Id
            );
            """,
            new { request.Code, Id = id },
            transaction,
            cancellationToken: cancellationToken));
        if (codeExists)
        {
            return new RoleWriteResult(RoleWriteStatus.CodeAlreadyExists);
        }

        var validPermissionIds = await GetValidPermissionIdsAsync(
            connection,
            transaction,
            request.PermissionIds,
            cancellationToken);
        if (validPermissionIds.Count != request.PermissionIds.Count)
        {
            return new RoleWriteResult(RoleWriteStatus.PermissionsNotFound);
        }

        var currentPermissionIds = (await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT permission_id FROM auth.role_permissions WHERE role_id = @Id;",
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken))).ToHashSet();
        var requestedPermissionIds = request.PermissionIds.ToHashSet();
        var addedPermissionIds = requestedPermissionIds.Except(currentPermissionIds).ToArray();
        var removedPermissionIds = currentPermissionIds.Except(requestedPermissionIds).ToArray();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE auth.roles
                SET code = @Code,
                    name = @Name,
                    description = @Description,
                    is_active = @IsActive,
                    updated_at = NOW(),
                    updated_by = @ActorId
                WHERE id = @Id
                  AND deleted_at IS NULL;
                """,
                new
                {
                    Id = id,
                    request.Code,
                    request.Name,
                    request.Description,
                    request.IsActive,
                    ActorId = actorId
                },
                transaction,
                cancellationToken: cancellationToken));

            if (addedPermissionIds.Length > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO auth.role_permissions (role_id, permission_id)
                    SELECT @RoleId, permission_id
                    FROM UNNEST(@PermissionIds) AS permission_id
                    ON CONFLICT (role_id, permission_id) DO NOTHING;
                    """,
                    new { RoleId = id, PermissionIds = addedPermissionIds },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            if (removedPermissionIds.Length > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    DELETE FROM auth.role_permissions
                    WHERE role_id = @RoleId
                      AND permission_id = ANY(@PermissionIds);
                    """,
                    new { RoleId = id, PermissionIds = removedPermissionIds },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            var role = await GetByIdWithPermissionsAsync(connection, transaction, id, cancellationToken);
            transaction.Commit();
            return new RoleWriteResult(RoleWriteStatus.Success, role);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            transaction.Rollback();
            return new RoleWriteResult(RoleWriteStatus.CodeAlreadyExists);
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.ForeignKeyViolation &&
            string.Equals(exception.ConstraintName, "role_permissions_permission_id_fkey", StringComparison.Ordinal))
        {
            transaction.Rollback();
            return new RoleWriteResult(RoleWriteStatus.PermissionsNotFound);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<RoleDeleteStatus> DeleteAsync(
        Guid id,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        using var connection = sqlConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var exists = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(
                "SELECT id FROM auth.roles WHERE id = @Id AND deleted_at IS NULL FOR UPDATE;",
                new { Id = id },
                transaction,
                cancellationToken: cancellationToken));
        if (exists is null)
        {
            return RoleDeleteStatus.NotFound;
        }

        var isAssignedToActiveUser = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM auth.user_roles ur
                INNER JOIN auth.users u ON u.id = ur.user_id
                WHERE ur.role_id = @Id
                  AND u.deleted_at IS NULL
                  AND u.is_active = TRUE
            );
            """,
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken));
        if (isAssignedToActiveUser)
        {
            return RoleDeleteStatus.InUse;
        }

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE auth.roles
                SET is_active = FALSE,
                    deleted_at = NOW(),
                    deleted_by = @ActorId,
                    updated_at = NOW(),
                    updated_by = @ActorId
                WHERE id = @Id
                  AND deleted_at IS NULL;
                """,
                new { Id = id, ActorId = actorId },
                transaction,
                cancellationToken: cancellationToken));

            transaction.Commit();
            return RoleDeleteStatus.Deleted;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static DynamicParameters CreateParameters(RoleSearchRequest request)
    {
        var parameters = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            parameters.Add("Keyword", $"%{request.Keyword}%");
        }

        if (request.IsActive.HasValue)
        {
            parameters.Add("IsActive", request.IsActive.Value);
        }

        return parameters;
    }

    private static void AppendBaseQuery(StringBuilder sql)
    {
        sql.AppendLine("""
            WITH role_data AS
            (
                SELECT
                    r.id AS Id,
                    r.code AS Code,
                    r.name AS Name,
                    r.description AS Description,
                    r.is_active AS IsActive,
                    r.created_at AS CreatedAt,
                    r.updated_at AS UpdatedAt,
                    COUNT(DISTINCT ur.user_id) AS UserCount,
                    COUNT(DISTINCT rp.permission_id) AS PermissionCount
                FROM auth.roles r
                LEFT JOIN auth.user_roles ur ON ur.role_id = r.id
                LEFT JOIN auth.role_permissions rp ON rp.role_id = r.id
                WHERE r.deleted_at IS NULL
                GROUP BY
                    r.id,
                    r.code,
                    r.name,
                    r.description,
                    r.is_active,
                    r.created_at,
                    r.updated_at
            )
            """);
    }

    private static void AppendFilters(StringBuilder sql, DynamicParameters parameters, RoleSearchRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            sql.AppendLine("AND (Code ILIKE @Keyword OR Name ILIKE @Keyword OR Description ILIKE @Keyword)");
        }

        if (request.IsActive.HasValue)
        {
            sql.AppendLine("AND IsActive = @IsActive");
        }
    }

    private static string GetSortExpression(string sortBy) => sortBy switch
    {
        "oldest" => "ORDER BY CreatedAt ASC, Id ASC",
        "name_asc" => "ORDER BY Name ASC, Id ASC",
        "name_desc" => "ORDER BY Name DESC, Id DESC",
        "code_asc" => "ORDER BY Code ASC, Id ASC",
        "code_desc" => "ORDER BY Code DESC, Id DESC",
        _ => "ORDER BY CreatedAt DESC, Id DESC"
    };

    public async Task<Guid?> GetRoleIdByCodeAsync(
        string roleCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT r.id
            FROM auth.roles r
            WHERE r.code = @RoleCode
              AND r.is_active = TRUE
              AND r.deleted_at IS NULL
            LIMIT 1;
            """;

        using var connection = sqlConnectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(sql, new { RoleCode = roleCode }, cancellationToken: cancellationToken));
    }

    private static async Task<IReadOnlyList<Guid>> GetValidPermissionIdsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        IReadOnlyCollection<Guid> permissionIds,
        CancellationToken cancellationToken)
    {
        if (permissionIds.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var ids = await connection.QueryAsync<Guid>(new CommandDefinition(
            """
            SELECT id
            FROM auth.permissions
            WHERE id = ANY(@PermissionIds)
            FOR KEY SHARE;
            """,
            new { PermissionIds = permissionIds.ToArray() },
            transaction,
            cancellationToken: cancellationToken));
        return ids.AsList();
    }

    private static async Task<RoleResponse?> GetByIdWithPermissionsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        Guid id,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                r.id AS Id,
                r.code AS Code,
                r.name AS Name,
                r.description AS Description,
                r.is_active AS IsActive,
                (SELECT COUNT(DISTINCT ur.user_id)::int FROM auth.user_roles ur WHERE ur.role_id = r.id) AS UserCount,
                (SELECT COUNT(DISTINCT rp.permission_id)::int FROM auth.role_permissions rp WHERE rp.role_id = r.id) AS PermissionCount,
                r.created_at AS CreatedAt,
                r.updated_at AS UpdatedAt
            FROM auth.roles r
            WHERE r.id = @Id
              AND r.deleted_at IS NULL;
            """;

        var role = await connection.QuerySingleOrDefaultAsync<RoleResponse>(
            new CommandDefinition(sql, new { Id = id }, transaction, cancellationToken: cancellationToken));
        if (role is null)
        {
            return null;
        }

        var permissions = await connection.QueryAsync<PermissionSummaryResponse>(new CommandDefinition(
            """
            SELECT p.id AS Id, p.code AS Code, p.name AS Name, p.description AS Description
            FROM auth.role_permissions rp
            INNER JOIN auth.permissions p ON p.id = rp.permission_id
            WHERE rp.role_id = @Id
            ORDER BY p.code, p.id;
            """,
            new { Id = id },
            transaction,
            cancellationToken: cancellationToken));
        role.Permissions = permissions.AsList();
        return role;
    }

    private static async Task LoadPermissionsAsync(
        IDbConnection connection,
        IReadOnlyCollection<RoleResponse> roles,
        CancellationToken cancellationToken)
    {
        if (roles.Count == 0)
        {
            return;
        }

        var roleIds = roles.Select(role => role.Id).ToArray();
        var permissions = await connection.QueryAsync<RolePermissionRow>(new CommandDefinition(
            """
            SELECT
                rp.role_id AS RoleId,
                p.id AS Id,
                p.code AS Code,
                p.name AS Name,
                p.description AS Description
            FROM auth.role_permissions rp
            INNER JOIN auth.permissions p ON p.id = rp.permission_id
            WHERE rp.role_id = ANY(@RoleIds)
            ORDER BY rp.role_id, p.code, p.id;
            """,
            new { RoleIds = roleIds },
            cancellationToken: cancellationToken));

        var permissionsByRoleId = permissions
            .GroupBy(permission => permission.RoleId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PermissionSummaryResponse>)group
                    .Select(permission => new PermissionSummaryResponse
                    {
                        Id = permission.Id,
                        Code = permission.Code,
                        Name = permission.Name,
                        Description = permission.Description
                    })
                    .DistinctBy(permission => permission.Id)
                    .ToArray());

        foreach (var role in roles)
        {
            role.Permissions = permissionsByRoleId.TryGetValue(role.Id, out var rolePermissions)
                ? rolePermissions
                : Array.Empty<PermissionSummaryResponse>();
        }
    }

    private sealed class RolePermissionRow
    {
        public Guid RoleId { get; set; }

        public Guid Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}