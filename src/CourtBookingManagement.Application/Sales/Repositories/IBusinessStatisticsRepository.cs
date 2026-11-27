using CourtBookingManagement.Application.Sales.Models.Responses;
using CourtBookingManagement.Application.Sales.Models.Requests;

namespace CourtBookingManagement.Application.Sales.Repositories;

public interface IBusinessStatisticsRepository
{
    Task<IReadOnlyList<BusinessStatisticsBranch>> GetBranchesAsync(Guid userId, CancellationToken cancellationToken);

    Task<BusinessStatisticsResponse> GetStatisticsAsync(
        Guid[] branchIds, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<IReadOnlyList<BookingStatisticsDetail>> GetBookingDetailsAsync(
        Guid[] branchIds, DateOnly date, CancellationToken cancellationToken);
    Task<IReadOnlyList<SalesStatisticsDetail>> GetSalesDetailsAsync(
        Guid[] branchIds, DateOnly date, Guid productId, CancellationToken cancellationToken);
    Task<Guid?> SaveExpenseAsync(
        Guid? id, Guid[] branchIds, Guid userId, BusinessExpenseRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteExpenseAsync(Guid id, Guid[] branchIds, CancellationToken cancellationToken);
}
