using CourtBookingManagement.Application.Catalog.Models.Requests;
using CourtBookingManagement.Application.Catalog.Models.Responses;
using CourtBookingManagement.Application.Matching.Models;

namespace CourtBookingManagement.Application.Catalog.Repositories;

public interface ICatalogRepository
{
    Task<IReadOnlyList<ProductCategoryResponse>> SearchCategoriesAsync(
        CategorySearchRequest request,
        CancellationToken cancellationToken);

    Task<ProductCategoryResponse?> GetCategoryAsync(Guid id, CancellationToken cancellationToken);

    Task<bool?> IsCategoryActiveAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> CategoryCodeExistsAsync(string code, Guid? excludeId, CancellationToken cancellationToken);

    Task<Guid> CreateCategoryAsync(
        CreateProductCategoryRequest request,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<bool> UpdateCategoryAsync(
        Guid id,
        UpdateProductCategoryRequest request,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<bool> DeactivateCategoryAsync(Guid id, Guid actorId, CancellationToken cancellationToken);

    Task<PagedResult<ProductResponse>> SearchProductsAsync(
        ProductSearchRequest request,
        CancellationToken cancellationToken);

    Task<ProductResponse?> GetProductAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> SkuExistsAsync(string sku, Guid? excludeId, CancellationToken cancellationToken);

    Task<bool> BarcodeExistsAsync(string barcode, Guid? excludeId, CancellationToken cancellationToken);

    Task<Guid> CreateProductAsync(CreateProductRequest request, Guid actorId, CancellationToken cancellationToken);

    Task<bool> UpdateProductAsync(
        Guid id,
        UpdateProductRequest request,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<bool> DeactivateProductAsync(Guid id, Guid actorId, CancellationToken cancellationToken);

    Task<bool> BranchExistsAsync(Guid branchId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductConfigurationState>> GetProductConfigurationStatesAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BranchProductResponse>> GetBranchProductsAsync(
        Guid branchId,
        bool? isAvailableForSale,
        CancellationToken cancellationToken);

    Task<BranchProductResponse?> GetBranchProductAsync(
        Guid branchId,
        Guid productId,
        CancellationToken cancellationToken);

    Task UpsertBranchProductsAsync(
        Guid branchId,
        IReadOnlyCollection<BranchProductUpsert> items,
        Guid actorId,
        CancellationToken cancellationToken);
}

public sealed record ProductConfigurationState(Guid ProductId, bool IsActive, bool IsCategoryActive);

public sealed record BranchProductUpsert(
    Guid ProductId,
    decimal SellingPrice,
    decimal? CostPrice,
    bool IsAvailableForSale,
    int DisplayOrder);
