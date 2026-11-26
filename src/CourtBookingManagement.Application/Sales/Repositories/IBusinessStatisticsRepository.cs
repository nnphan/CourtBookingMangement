using CourtBookingManagement.Application.Sales.Models.Responses;

namespace CourtBookingManagement.Application.Sales.Repositories;

public interface IBusinessStatisticsRepository
{
    Task<IReadOnlyList<BusinessStatisticsBranch>> GetBranchesAsync(Guid userId, CancellationToken cancellationToken);

    Task<BusinessStatisticsResponse> GetStatisticsAsync(
        Guid[] branchIds, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
