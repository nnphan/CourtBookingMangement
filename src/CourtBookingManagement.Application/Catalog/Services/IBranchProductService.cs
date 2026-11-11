using CourtBookingManagement.Application.Catalog.Models.Requests;
using CourtBookingManagement.Application.Catalog.Models.Responses;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Catalog.Services;

public interface IBranchProductService
{
    Task<Result<IReadOnlyList<BranchProductResponse>>> GetProductsAsync(
        Guid userId,
        Guid branchId,
        bool? isAvailableForSale,
        CancellationToken cancellationToken);

    Task<Result<BranchProductResponse>> GetProductAsync(
        Guid userId,
        Guid branchId,
        Guid productId,
        CancellationToken cancellationToken);

    Task<Result<BranchProductResponse>> ConfigureProductAsync(
        Guid userId,
        Guid branchId,
        Guid productId,
        BranchProductConfigurationRequest request,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<BranchProductResponse>>> BulkConfigureAsync(
        Guid userId,
        Guid branchId,
        BulkBranchProductConfigurationRequest request,
        CancellationToken cancellationToken);
}
