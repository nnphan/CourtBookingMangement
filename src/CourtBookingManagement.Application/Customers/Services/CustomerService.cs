using CourtBookingManagement.Application.Customers.Models.Requests;
using CourtBookingManagement.Application.Customers.Models.Responses;
using CourtBookingManagement.Application.Customers.Repositories;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Domain.Abstractions;

namespace CourtBookingManagement.Application.Customers.Services;

public sealed class CustomerService(ICustomerRepository repository) : ICustomerService
{
    public async Task<Result<PagedResult<CustomerListItemResponse>>> SearchAsync(
        CustomerSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Failure<PagedResult<CustomerListItemResponse>>(
                new Error("Customer.InvalidRequest", "Request cannot be null."));
        }

        if (request.PageNumber < 1)
        {
            return Result.Failure<PagedResult<CustomerListItemResponse>>(
                new Error("Customer.InvalidPage", "PageNumber must be greater than or equal to 1."));
        }

        if (request.PageSize is < 1 or > 100)
        {
            return Result.Failure<PagedResult<CustomerListItemResponse>>(
                new Error("Customer.InvalidPageSize", "PageSize must be between 1 and 100."));
        }

        var sortBy = NormalizeSort(request.SortBy);
        if (sortBy is null)
        {
            return Result.Failure<PagedResult<CustomerListItemResponse>>(
                new Error(
                    "Customer.InvalidSort",
                    "SortBy must be latest, oldest, name_asc, name_desc, points_asc, or points_desc."));
        }

        var normalizedRequest = new CustomerSearchRequest
        {
            Keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim(),
            IsActive = request.IsActive,
            IsGuest = request.IsGuest,
            MembershipLevelId = request.MembershipLevelId,
            SortBy = sortBy,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        try
        {
            return Result.Success(await repository.SearchAsync(normalizedRequest, cancellationToken));
        }
        catch (Exception exception)
        {
            return Result.Failure<PagedResult<CustomerListItemResponse>>(Error.FromException(exception));
        }
    }

    public async Task<Result<CustomerDetailResponse>> GetByIdAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        if (customerId == Guid.Empty)
        {
            return Result.Failure<CustomerDetailResponse>(
                new Error("Customer.InvalidId", "A valid customer id is required."));
        }

        try
        {
            var customer = await repository.GetByIdAsync(customerId, cancellationToken);
            return customer is null
                ? Result.Failure<CustomerDetailResponse>(new Error("Customer.NotFound", "Customer not found."))
                : Result.Success(customer);
        }
        catch (Exception exception)
        {
            return Result.Failure<CustomerDetailResponse>(Error.FromException(exception));
        }
    }

    private static string? NormalizeSort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "latest";
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "latest" => "latest",
            "oldest" => "oldest",
            "name_asc" => "name_asc",
            "name_desc" => "name_desc",
            "points_asc" => "points_asc",
            "points_desc" => "points_desc",
            _ => null
        };
    }
}
