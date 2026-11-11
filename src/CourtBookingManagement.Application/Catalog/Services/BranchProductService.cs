using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Catalog.Models.Requests;
using CourtBookingManagement.Application.Catalog.Models.Responses;
using CourtBookingManagement.Application.Catalog.Repositories;
using CourtBookingManagement.Domain.Abstractions;
using FluentValidation;

namespace CourtBookingManagement.Application.Catalog.Services;

public sealed class BranchProductService(
    ICatalogRepository repository,
    IBranchAuthorizationService branchAuthorizationService,
    IValidator<BranchProductConfigurationRequest> configurationValidator,
    IValidator<BulkBranchProductConfigurationRequest> bulkValidator) : IBranchProductService
{
    private static readonly Error BranchNotFound = new("Catalog.BranchNotFound", "Branch not found.");
    private static readonly Error ProductNotFound = new("Catalog.ProductNotFound", "Product not found.");
    private static readonly Error ProductInactive = new("Catalog.ProductInactive", "An inactive product or category cannot be enabled for sale.");
    private static readonly Error InvalidRequest = new("Catalog.InvalidRequest", "The request is invalid.");

    public async Task<Result<IReadOnlyList<BranchProductResponse>>> GetProductsAsync(
        Guid userId,
        Guid branchId,
        bool? isAvailableForSale,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || branchId == Guid.Empty)
        {
            return InvalidRequest;
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "product.view", cancellationToken);
        if (!await repository.BranchExistsAsync(branchId, cancellationToken))
        {
            return BranchNotFound;
        }

        return Result.Success(await repository.GetBranchProductsAsync(
            branchId,
            isAvailableForSale,
            cancellationToken));
    }

    public async Task<Result<BranchProductResponse>> GetProductAsync(
        Guid userId,
        Guid branchId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || branchId == Guid.Empty || productId == Guid.Empty)
        {
            return InvalidRequest;
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "product.view", cancellationToken);
        if (!await repository.BranchExistsAsync(branchId, cancellationToken))
        {
            return BranchNotFound;
        }

        var product = await repository.GetBranchProductAsync(branchId, productId, cancellationToken);
        return product is null ? ProductNotFound : Result.Success(product);
    }

    public async Task<Result<BranchProductResponse>> ConfigureProductAsync(
        Guid userId,
        Guid branchId,
        Guid productId,
        BranchProductConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        await configurationValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (userId == Guid.Empty || branchId == Guid.Empty || productId == Guid.Empty)
        {
            return InvalidRequest;
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "product.update", cancellationToken);
        if (!await repository.BranchExistsAsync(branchId, cancellationToken))
        {
            return BranchNotFound;
        }

        var state = await repository.GetProductConfigurationStatesAsync([productId], cancellationToken);
        if (state.Count != 1)
        {
            return ProductNotFound;
        }

        if (request.IsAvailableForSale && (!state[0].IsActive || !state[0].IsCategoryActive))
        {
            return ProductInactive;
        }

        var item = new BranchProductUpsert(
            productId,
            request.SellingPrice,
            request.CostPrice,
            request.IsAvailableForSale,
            request.DisplayOrder);
        await repository.UpsertBranchProductsAsync(branchId, [item], userId, cancellationToken);

        var product = await repository.GetBranchProductAsync(branchId, productId, cancellationToken);
        return product is null ? ProductNotFound : Result.Success(product);
    }

    public async Task<Result<IReadOnlyList<BranchProductResponse>>> BulkConfigureAsync(
        Guid userId,
        Guid branchId,
        BulkBranchProductConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        await bulkValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (userId == Guid.Empty || branchId == Guid.Empty)
        {
            return InvalidRequest;
        }

        await branchAuthorizationService.EnsureCanAccessAsync(userId, branchId, "product.update", cancellationToken);
        if (!await repository.BranchExistsAsync(branchId, cancellationToken))
        {
            return BranchNotFound;
        }

        var states = await repository.GetProductConfigurationStatesAsync(
            request.Items.Select(item => item.ProductId).ToArray(),
            cancellationToken);
        if (states.Count != request.Items.Count)
        {
            return ProductNotFound;
        }

        var statesById = states.ToDictionary(state => state.ProductId);
        if (request.Items.Any(item =>
                item.IsAvailableForSale &&
                (!statesById[item.ProductId].IsActive || !statesById[item.ProductId].IsCategoryActive)))
        {
            return ProductInactive;
        }

        var configurations = request.Items
            .Select(item => new BranchProductUpsert(
                item.ProductId,
                item.SellingPrice,
                item.CostPrice,
                item.IsAvailableForSale,
                item.DisplayOrder))
            .ToArray();
        await repository.UpsertBranchProductsAsync(branchId, configurations, userId, cancellationToken);

        return Result.Success(await repository.GetBranchProductsAsync(branchId, null, cancellationToken));
    }
}
