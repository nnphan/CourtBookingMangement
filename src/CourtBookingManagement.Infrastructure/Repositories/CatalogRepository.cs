using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Catalog.Exceptions;
using CourtBookingManagement.Application.Catalog.Models.Requests;
using CourtBookingManagement.Application.Catalog.Models.Responses;
using CourtBookingManagement.Application.Catalog.Repositories;
using CourtBookingManagement.Application.Matching.Models;
using Dapper;
using Npgsql;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class CatalogRepository(ISqlConnectionFactory connectionFactory) : ICatalogRepository
{
    private const string CategoryCodeConstraint = "product_categories_code_key";
    private const string ProductSkuConstraint = "products_sku_key";
    private const string ProductBarcodeConstraint = "ux_products_barcode";

    public async Task<IReadOnlyList<ProductCategoryResponse>> SearchCategoriesAsync(
        CategorySearchRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                code AS Code,
                name AS Name,
                description AS Description,
                display_order AS DisplayOrder,
                is_active AS IsActive,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM catalog.product_categories
            WHERE deleted_at IS NULL
              AND (@Search IS NULL OR name ILIKE @Search OR code ILIKE @Search)
              AND (@IsActive IS NULL OR is_active = @IsActive)
            ORDER BY display_order, name, id;
            """;

        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ProductCategoryResponse>(new CommandDefinition(
            sql,
            new
            {
                Search = string.IsNullOrWhiteSpace(request.Search) ? null : $"%{request.Search}%",
                request.IsActive
            },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<ProductCategoryResponse?> GetCategoryAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                code AS Code,
                name AS Name,
                description AS Description,
                display_order AS DisplayOrder,
                is_active AS IsActive,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM catalog.product_categories
            WHERE id = @Id
              AND deleted_at IS NULL;
            """;

        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ProductCategoryResponse>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<bool?> IsCategoryActiveAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT is_active
            FROM catalog.product_categories
            WHERE id = @Id
              AND deleted_at IS NULL;
            """;

        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool?>(new CommandDefinition(
            sql,
            new { Id = id },
            cancellationToken: cancellationToken));
    }

    public Task<bool> CategoryCodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM catalog.product_categories
                WHERE LOWER(code) = LOWER(@Code)
                  AND deleted_at IS NULL
                  AND (@ExcludeId::uuid IS NULL OR id <> @ExcludeId)
            );
            """,
            new { Code = code, ExcludeId = excludeId },
            cancellationToken);

    public async Task<Guid> CreateCategoryAsync(
        CreateProductCategoryRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO catalog.product_categories
                (code, name, description, display_order, is_active, created_by, updated_by)
            VALUES
                (@Code, @Name, @Description, @DisplayOrder, @IsActive, @ActorId, @ActorId)
            RETURNING id;
            """;

        using var connection = connectionFactory.CreateConnection();
        try
        {
            return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
                sql,
                new
                {
                    request.Code,
                    request.Name,
                    request.Description,
                    request.DisplayOrder,
                    request.IsActive,
                    ActorId = actorId
                },
                cancellationToken: cancellationToken));
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation &&
            exception.ConstraintName == CategoryCodeConstraint)
        {
            throw new CatalogDuplicateException(exception.ConstraintName);
        }
    }

    public async Task<bool> UpdateCategoryAsync(
        Guid id,
        UpdateProductCategoryRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE catalog.product_categories
            SET code = @Code,
                name = @Name,
                description = @Description,
                display_order = @DisplayOrder,
                is_active = @IsActive,
                updated_at = now(),
                updated_by = @ActorId
            WHERE id = @Id
              AND deleted_at IS NULL;
            """;

        using var connection = connectionFactory.CreateConnection();
        try
        {
            return await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    Id = id,
                    request.Code,
                    request.Name,
                    request.Description,
                    request.DisplayOrder,
                    request.IsActive,
                    ActorId = actorId
                },
                cancellationToken: cancellationToken)) == 1;
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation &&
            exception.ConstraintName == CategoryCodeConstraint)
        {
            throw new CatalogDuplicateException(exception.ConstraintName);
        }
    }

    public async Task<bool> DeactivateCategoryAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE catalog.product_categories
            SET is_active = FALSE,
                updated_at = now(),
                updated_by = @ActorId
            WHERE id = @Id
              AND deleted_at IS NULL;
            """;

        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { Id = id, ActorId = actorId },
            cancellationToken: cancellationToken)) == 1;
    }

    public async Task<PagedResult<ProductResponse>> SearchProductsAsync(
        ProductSearchRequest request,
        CancellationToken cancellationToken)
    {
        var filterSql = """
            FROM catalog.products p
            JOIN catalog.product_categories c ON c.id = p.category_id
            WHERE p.deleted_at IS NULL
              AND c.deleted_at IS NULL
              AND (@Search IS NULL OR p.name ILIKE @Search OR p.sku ILIKE @Search OR p.barcode ILIKE @Search)
              AND (@CategoryId::uuid IS NULL OR p.category_id = @CategoryId)
              AND (@IsActive IS NULL OR p.is_active = @IsActive)
              AND (@TrackInventory IS NULL OR p.track_inventory = @TrackInventory)
            """;
        var parameters = new
        {
            Search = string.IsNullOrWhiteSpace(request.Search) ? null : $"%{request.Search}%",
            request.CategoryId,
            request.IsActive,
            request.TrackInventory,
            request.PageSize,
            Offset = (long)(request.Page - 1) * request.PageSize
        };
        var sortSql = GetSortExpression(request.Sort);
        var dataSql = """
            SELECT
                p.id AS Id,
                p.category_id AS CategoryId,
                c.code AS CategoryCode,
                c.name AS CategoryName,
                p.sku AS Sku,
                p.barcode AS Barcode,
                p.name AS Name,
                p.description AS Description,
                p.unit AS Unit,
                p.default_cost_price AS DefaultCostPrice,
                p.default_selling_price AS DefaultSellingPrice,
                p.track_inventory AS TrackInventory,
                p.is_active AS IsActive,
                p.created_at AS CreatedAt,
                p.updated_at AS UpdatedAt
            """ + filterSql + sortSql + " LIMIT @PageSize OFFSET @Offset;";

        using var connection = connectionFactory.CreateConnection();
        var items = (await connection.QueryAsync<ProductResponse>(new CommandDefinition(
            dataSql,
            parameters,
            cancellationToken: cancellationToken))).AsList();
        var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(*) " + filterSql,
            parameters,
            cancellationToken: cancellationToken));

        return PagedResult<ProductResponse>.Create(items, request.Page, request.PageSize, count);
    }

    public async Task<ProductResponse?> GetProductAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.id AS Id,
                p.category_id AS CategoryId,
                c.code AS CategoryCode,
                c.name AS CategoryName,
                p.sku AS Sku,
                p.barcode AS Barcode,
                p.name AS Name,
                p.description AS Description,
                p.unit AS Unit,
                p.default_cost_price AS DefaultCostPrice,
                p.default_selling_price AS DefaultSellingPrice,
                p.track_inventory AS TrackInventory,
                p.is_active AS IsActive,
                p.created_at AS CreatedAt,
                p.updated_at AS UpdatedAt
            FROM catalog.products p
            JOIN catalog.product_categories c ON c.id = p.category_id
            WHERE p.id = @Id
              AND p.deleted_at IS NULL
              AND c.deleted_at IS NULL;
            """;

        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ProductResponse>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public Task<bool> SkuExistsAsync(string sku, Guid? excludeId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM catalog.products
                WHERE LOWER(sku) = LOWER(@Sku)
                  AND deleted_at IS NULL
                  AND (@ExcludeId::uuid IS NULL OR id <> @ExcludeId)
            );
            """,
            new { Sku = sku, ExcludeId = excludeId },
            cancellationToken);

    public Task<bool> BarcodeExistsAsync(string barcode, Guid? excludeId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM catalog.products
                WHERE barcode = @Barcode
                  AND deleted_at IS NULL
                  AND (@ExcludeId::uuid IS NULL OR id <> @ExcludeId)
            );
            """,
            new { Barcode = barcode, ExcludeId = excludeId },
            cancellationToken);

    public async Task<Guid> CreateProductAsync(
        CreateProductRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO catalog.products
                (category_id, sku, barcode, name, description, unit, default_cost_price,
                 default_selling_price, track_inventory, is_active, created_by, updated_by)
            VALUES
                (@CategoryId, @Sku, @Barcode, @Name, @Description, @Unit, @DefaultCostPrice,
                 @DefaultSellingPrice, @TrackInventory, @IsActive, @ActorId, @ActorId)
            RETURNING id;
            """;

        using var connection = connectionFactory.CreateConnection();
        try
        {
            return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
                sql,
                new
                {
                    request.CategoryId,
                    request.Sku,
                    request.Barcode,
                    request.Name,
                    request.Description,
                    request.Unit,
                    request.DefaultCostPrice,
                    request.DefaultSellingPrice,
                    request.TrackInventory,
                    request.IsActive,
                    ActorId = actorId
                },
                cancellationToken: cancellationToken));
        }
        catch (PostgresException exception) when (IsProductUniqueViolation(exception))
        {
            throw new CatalogDuplicateException(exception.ConstraintName ?? ProductSkuConstraint);
        }
    }

    public async Task<bool> UpdateProductAsync(
        Guid id,
        UpdateProductRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE catalog.products
            SET category_id = @CategoryId,
                sku = @Sku,
                barcode = @Barcode,
                name = @Name,
                description = @Description,
                unit = @Unit,
                default_cost_price = @DefaultCostPrice,
                default_selling_price = @DefaultSellingPrice,
                track_inventory = @TrackInventory,
                is_active = @IsActive,
                updated_at = now(),
                updated_by = @ActorId
            WHERE id = @Id
              AND deleted_at IS NULL;
            """;

        using var connection = connectionFactory.CreateConnection();
        try
        {
            return await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    Id = id,
                    request.CategoryId,
                    request.Sku,
                    request.Barcode,
                    request.Name,
                    request.Description,
                    request.Unit,
                    request.DefaultCostPrice,
                    request.DefaultSellingPrice,
                    request.TrackInventory,
                    request.IsActive,
                    ActorId = actorId
                },
                cancellationToken: cancellationToken)) == 1;
        }
        catch (PostgresException exception) when (IsProductUniqueViolation(exception))
        {
            throw new CatalogDuplicateException(exception.ConstraintName ?? ProductSkuConstraint);
        }
    }

    public async Task<bool> DeactivateProductAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE catalog.products
            SET is_active = FALSE,
                updated_at = now(),
                updated_by = @ActorId
            WHERE id = @Id
              AND deleted_at IS NULL;
            """;

        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { Id = id, ActorId = actorId },
            cancellationToken: cancellationToken)) == 1;
    }

    public Task<bool> BranchExistsAsync(Guid branchId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM core.branches WHERE id = @BranchId AND is_active AND deleted_at IS NULL);",
            new { BranchId = branchId },
            cancellationToken);

    public async Task<IReadOnlyList<ProductConfigurationState>> GetProductConfigurationStatesAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.id AS ProductId,
                p.is_active AS IsActive,
                (c.is_active AND c.deleted_at IS NULL) AS IsCategoryActive
            FROM catalog.products p
            JOIN catalog.product_categories c ON c.id = p.category_id
            WHERE p.id = ANY(@ProductIds)
              AND p.deleted_at IS NULL;
            """;

        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ProductConfigurationState>(new CommandDefinition(
            sql,
            new { ProductIds = productIds.ToArray() },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<BranchProductResponse>> GetBranchProductsAsync(
        Guid branchId,
        bool? isAvailableForSale,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.id AS ProductId,
                p.category_id AS CategoryId,
                c.name AS CategoryName,
                p.sku AS Sku,
                p.barcode AS Barcode,
                p.name AS Name,
                p.description AS Description,
                p.unit AS Unit,
                p.track_inventory AS TrackInventory,
                bp.selling_price AS SellingPrice,
                bp.cost_price AS CostPrice,
                bp.is_available_for_sale AS IsAvailableForSale,
                bp.display_order AS DisplayOrder,
                bp.updated_at AS UpdatedAt
            FROM catalog.branch_products bp
            JOIN catalog.products p ON p.id = bp.product_id
            JOIN catalog.product_categories c ON c.id = p.category_id
            WHERE bp.branch_id = @BranchId
              AND p.is_active
              AND p.deleted_at IS NULL
              AND c.is_active
              AND c.deleted_at IS NULL
              AND (@IsAvailableForSale IS NULL OR bp.is_available_for_sale = @IsAvailableForSale)
            ORDER BY bp.display_order, p.name, p.id;
            """;

        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<BranchProductResponse>(new CommandDefinition(
            sql,
            new { BranchId = branchId, IsAvailableForSale = isAvailableForSale },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<BranchProductResponse?> GetBranchProductAsync(
        Guid branchId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.id AS ProductId,
                p.category_id AS CategoryId,
                c.name AS CategoryName,
                p.sku AS Sku,
                p.barcode AS Barcode,
                p.name AS Name,
                p.description AS Description,
                p.unit AS Unit,
                p.track_inventory AS TrackInventory,
                bp.selling_price AS SellingPrice,
                bp.cost_price AS CostPrice,
                bp.is_available_for_sale AS IsAvailableForSale,
                bp.display_order AS DisplayOrder,
                bp.updated_at AS UpdatedAt
            FROM catalog.branch_products bp
            JOIN catalog.products p ON p.id = bp.product_id
            JOIN catalog.product_categories c ON c.id = p.category_id
            WHERE bp.branch_id = @BranchId
              AND bp.product_id = @ProductId
              AND p.deleted_at IS NULL
              AND c.deleted_at IS NULL;
            """;

        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<BranchProductResponse>(new CommandDefinition(
            sql,
            new { BranchId = branchId, ProductId = productId },
            cancellationToken: cancellationToken));
    }

    public async Task UpsertBranchProductsAsync(
        Guid branchId,
        IReadOnlyCollection<BranchProductUpsert> items,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        try
        {
            foreach (var item in items.OrderBy(item => item.ProductId))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO catalog.branch_products
                        (branch_id, product_id, selling_price, cost_price, is_available_for_sale,
                         display_order, created_by, updated_by)
                    VALUES
                        (@BranchId, @ProductId, @SellingPrice, @CostPrice, @IsAvailableForSale,
                         @DisplayOrder, @ActorId, @ActorId)
                    ON CONFLICT (branch_id, product_id) DO UPDATE
                    SET selling_price = EXCLUDED.selling_price,
                        cost_price = EXCLUDED.cost_price,
                        is_available_for_sale = EXCLUDED.is_available_for_sale,
                        display_order = EXCLUDED.display_order,
                        updated_at = now(),
                        updated_by = EXCLUDED.updated_by;
                    """,
                    new
                    {
                        BranchId = branchId,
                        item.ProductId,
                        item.SellingPrice,
                        item.CostPrice,
                        item.IsAvailableForSale,
                        item.DisplayOrder,
                        ActorId = actorId
                    },
                    transaction,
                    cancellationToken: cancellationToken));

                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO inventory.branch_inventory
                        (branch_id, product_id, quantity_on_hand, quantity_reserved, reorder_level, updated_by)
                    SELECT @BranchId, p.id, 0, 0, 0, @ActorId
                    FROM catalog.products p
                    WHERE p.id = @ProductId
                      AND p.track_inventory
                    ON CONFLICT (branch_id, product_id) DO NOTHING;
                    """,
                    new { BranchId = branchId, item.ProductId, ActorId = actorId },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private async Task<T> ScalarAsync<T>(
        string sql,
        object parameters,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<T>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    private static bool IsProductUniqueViolation(PostgresException exception) =>
        exception.SqlState == PostgresErrorCodes.UniqueViolation &&
        exception.ConstraintName is ProductSkuConstraint or ProductBarcodeConstraint;

    private static string GetSortExpression(string sort) => sort switch
    {
        "name_desc" => " ORDER BY p.name DESC, p.id DESC",
        "sku_asc" => " ORDER BY p.sku ASC, p.id ASC",
        "price_asc" => " ORDER BY p.default_selling_price ASC, p.id ASC",
        "price_desc" => " ORDER BY p.default_selling_price DESC, p.id DESC",
        _ => " ORDER BY p.name ASC, p.id ASC"
    };
}
