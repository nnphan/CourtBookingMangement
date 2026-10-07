using System.Data;
using CourtBookingManagement.Application.Customers.Models.Requests;
using CourtBookingManagement.Application.Customers.Models.Responses;
using CourtBookingManagement.Application.Matching.Models;

namespace CourtBookingManagement.Application.Customers.Repositories;

public interface ICustomerRepository
{
    Task<PagedResult<CustomerListItemResponse>> SearchAsync(
        CustomerSearchRequest request,
        CancellationToken cancellationToken);

    Task<CustomerDetailResponse?> GetByIdAsync(
        Guid customerId,
        CancellationToken cancellationToken);

    Task<bool> ExistsByUserIdAsync(
        Guid userId,
        IDbTransaction transaction,
        CancellationToken cancellationToken);

    Task<Guid> CreateCustomerAsync(
        CreateCustomerInternalRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken);
}