using CourtBookingManagement.Application.Catalog.Exceptions;
using CourtBookingManagement.Application.Catalog.Models.Requests;
using CourtBookingManagement.Application.Catalog.Models.Responses;
using CourtBookingManagement.Application.Catalog.Repositories;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;

namespace CourtBookingManagement.Application.Catalog.Services;

public sealed class CatalogService(
    ICatalogRepository repository,
    IValidator<CreateProductCategoryRequest> createCategoryValidator,
    IValidator<UpdateProductCategoryRequest> updateCategoryValidator,
    IValidator<CreateProductRequest> createProductValidator,
    IValidator<UpdateProductRequest> updateProductValidator) : ICatalogService
{
    private static readonly Error CategoryNotFound = new("Catalog.CategoryNotFound", "Product category not found.");
    private static readonly Error CategoryInactive = new("Catalog.CategoryInactive", "An active product requires an active category.");
    private static readonly Error CategoryCodeExists = new("Catalog.CategoryCodeExists", "A product category with this code already exists.");
    private static readonly Error ProductNotFound = new("Catalog.ProductNotFound", "Product not found.");
    private static readonly Error ProductInactive = new("Catalog.ProductInactive", "Product is inactive.");
    private static readonly Error ProductSkuExists = new("Catalog.ProductSkuExists", "A product with this SKU already exists.");
    private static readonly Error ProductBarcodeExists = new("Catalog.ProductBarcodeExists", "A product with this barcode already exists.");
    private static readonly Error InvalidRequest = new("Catalog.InvalidRequest", "The request is invalid.");

    public async Task<Result<IReadOnlyList<ProductCategoryResponse>>> SearchCategoriesAsync(
        CategorySearchRequest request,
        CancellationToken cancellationToken)
    {
        var categories = await repository.SearchCategoriesAsync(
            request with { Search = NormalizeOptional(request.Search) },
            cancellationToken);
        return Result.Success(categories);
    }

    public async Task<Result<ProductCategoryResponse>> GetCategoryAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return InvalidRequest;
        }

        var category = await repository.GetCategoryAsync(id, cancellationToken);
        return category is null ? CategoryNotFound : Result.Success(category);
    }

    public async Task<Result<ProductCategoryResponse>> CreateCategoryAsync(
        CreateProductCategoryRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        await createCategoryValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (actorId == Guid.Empty)
        {
            return InvalidRequest;
        }

        var normalized = request with
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description)
        };
        if (await repository.CategoryCodeExistsAsync(normalized.Code, null, cancellationToken))
        {
            return CategoryCodeExists;
        }

        try
        {
            var id = await repository.CreateCategoryAsync(normalized, actorId, cancellationToken);
            var category = await repository.GetCategoryAsync(id, cancellationToken);
            return category is null ? CategoryNotFound : Result.Success(category);
        }
        catch (CatalogDuplicateException exception) when (exception.ConstraintName.Contains("code", StringComparison.OrdinalIgnoreCase))
        {
            return CategoryCodeExists;
        }
    }

    public async Task<Result<ProductCategoryResponse>> UpdateCategoryAsync(
        Guid id,
        UpdateProductCategoryRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        await updateCategoryValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (id == Guid.Empty || actorId == Guid.Empty)
        {
            return InvalidRequest;
        }

        if (await repository.GetCategoryAsync(id, cancellationToken) is null)
        {
            return CategoryNotFound;
        }

        var normalized = request with
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description)
        };
        if (await repository.CategoryCodeExistsAsync(normalized.Code, id, cancellationToken))
        {
            return CategoryCodeExists;
        }

        try
        {
            if (!await repository.UpdateCategoryAsync(id, normalized, actorId, cancellationToken))
            {
                return CategoryNotFound;
            }

            var category = await repository.GetCategoryAsync(id, cancellationToken);
            return category is null ? CategoryNotFound : Result.Success(category);
        }
        catch (CatalogDuplicateException exception) when (exception.ConstraintName.Contains("code", StringComparison.OrdinalIgnoreCase))
        {
            return CategoryCodeExists;
        }
    }

    public async Task<Result> DeactivateCategoryAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || actorId == Guid.Empty)
        {
            return Result.Failure(InvalidRequest);
        }

        return await repository.DeactivateCategoryAsync(id, actorId, cancellationToken)
            ? Result.Success()
            : Result.Failure(CategoryNotFound);
    }

    public async Task<Result<PagedResult<ProductResponse>>> SearchProductsAsync(
        ProductSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100)
        {
            return new Error("Catalog.InvalidPagination", "Page must be at least 1 and pageSize must be between 1 and 100.");
        }

        var sort = NormalizeSort(request.Sort);
        if (sort is null)
        {
            return new Error("Catalog.InvalidSort", "Sort must be name_asc, name_desc, sku_asc, price_asc, or price_desc.");
        }

        var normalized = request with { Search = NormalizeOptional(request.Search), Sort = sort };
        return Result.Success(await repository.SearchProductsAsync(normalized, cancellationToken));
    }

    public async Task<Result<ProductResponse>> GetProductAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return InvalidRequest;
        }

        var product = await repository.GetProductAsync(id, cancellationToken);
        return product is null ? ProductNotFound : Result.Success(product);
    }

    public async Task<Result<ProductResponse>> CreateProductAsync(
        CreateProductRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        await createProductValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (actorId == Guid.Empty)
        {
            return InvalidRequest;
        }

        var categoryState = await repository.IsCategoryActiveAsync(request.CategoryId, cancellationToken);
        if (categoryState is null)
        {
            return CategoryNotFound;
        }

        if (!categoryState.Value)
        {
            return CategoryInactive;
        }

        var normalized = request with
        {
            Sku = request.Sku.Trim().ToUpperInvariant(),
            Barcode = NormalizeOptional(request.Barcode),
            Name = request.Name.Trim(),
            Unit = request.Unit.Trim(),
            Description = NormalizeOptional(request.Description)
        };
        if (await repository.SkuExistsAsync(normalized.Sku, null, cancellationToken))
        {
            return ProductSkuExists;
        }

        if (normalized.Barcode is not null &&
            await repository.BarcodeExistsAsync(normalized.Barcode, null, cancellationToken))
        {
            return ProductBarcodeExists;
        }

        try
        {
            var id = await repository.CreateProductAsync(normalized, actorId, cancellationToken);
            var product = await repository.GetProductAsync(id, cancellationToken);
            return product is null ? ProductNotFound : Result.Success(product);
        }
        catch (CatalogDuplicateException exception)
        {
            return MapProductDuplicate(exception);
        }
    }

    public async Task<Result<ProductResponse>> UpdateProductAsync(
        Guid id,
        UpdateProductRequest request,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        await updateProductValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (id == Guid.Empty || actorId == Guid.Empty)
        {
            return InvalidRequest;
        }

        if (await repository.GetProductAsync(id, cancellationToken) is null)
        {
            return ProductNotFound;
        }

        var categoryState = await repository.IsCategoryActiveAsync(request.CategoryId, cancellationToken);
        if (categoryState is null)
        {
            return CategoryNotFound;
        }

        if (request.IsActive && !categoryState.Value)
        {
            return CategoryInactive;
        }

        var normalized = request with
        {
            Sku = request.Sku.Trim().ToUpperInvariant(),
            Barcode = NormalizeOptional(request.Barcode),
            Name = request.Name.Trim(),
            Unit = request.Unit.Trim(),
            Description = NormalizeOptional(request.Description)
        };
        if (await repository.SkuExistsAsync(normalized.Sku, id, cancellationToken))
        {
            return ProductSkuExists;
        }

        if (normalized.Barcode is not null &&
            await repository.BarcodeExistsAsync(normalized.Barcode, id, cancellationToken))
        {
            return ProductBarcodeExists;
        }

        try
        {
            if (!await repository.UpdateProductAsync(id, normalized, actorId, cancellationToken))
            {
                return ProductNotFound;
            }

            var product = await repository.GetProductAsync(id, cancellationToken);
            return product is null ? ProductNotFound : Result.Success(product);
        }
        catch (CatalogDuplicateException exception)
        {
            return MapProductDuplicate(exception);
        }
    }

    public async Task<Result> DeactivateProductAsync(Guid id, Guid actorId, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || actorId == Guid.Empty)
        {
            return Result.Failure(InvalidRequest);
        }

        return await repository.DeactivateProductAsync(id, actorId, cancellationToken)
            ? Result.Success()
            : Result.Failure(ProductNotFound);
    }

    private static Error MapProductDuplicate(CatalogDuplicateException exception) =>
        exception.ConstraintName.Contains("barcode", StringComparison.OrdinalIgnoreCase)
            ? ProductBarcodeExists
            : ProductSkuExists;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeSort(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "name_asc" => "name_asc",
        "name_desc" => "name_desc",
        "sku_asc" => "sku_asc",
        "price_asc" => "price_asc",
        "price_desc" => "price_desc",
        _ => null
    };
}
