using CourtBookingManagement.Application.Catalog.Models.Requests;
using CourtBookingManagement.Application.Catalog.Models.Responses;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Catalog.Services;

public interface ICatalogService
{
    Task<Result<IReadOnlyList<ProductCategoryResponse>>> SearchCategoriesAsync(
        CategorySearchRequest request,
        CancellationToken cancellationToken);

    Task<Result<ProductCategoryResponse>> GetCategoryAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<ProductCategoryResponse>> CreateCategoryAsync(
        CreateProductCategoryRequest request,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<Result<ProductCategoryResponse>> UpdateCategoryAsync(
        Guid id,
        UpdateProductCategoryRequest request,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<Result> DeactivateCategoryAsync(Guid id, Guid actorId, CancellationToken cancellationToken);

    Task<Result<PagedResult<ProductResponse>>> SearchProductsAsync(
        ProductSearchRequest request,
        CancellationToken cancellationToken);

    Task<Result<ProductResponse>> GetProductAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<ProductResponse>> CreateProductAsync(
        CreateProductRequest request,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<Result<ProductResponse>> UpdateProductAsync(
        Guid id,
        UpdateProductRequest request,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<Result> DeactivateProductAsync(Guid id, Guid actorId, CancellationToken cancellationToken);
}
