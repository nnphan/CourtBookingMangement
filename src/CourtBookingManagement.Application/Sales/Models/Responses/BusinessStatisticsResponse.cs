namespace CourtBookingManagement.Application.Sales.Models.Responses;

public sealed record BusinessStatisticsResponse(
    IReadOnlyList<BookingStatisticsDay> BookingDays,
    IReadOnlyList<SalesStatisticsDay> SalesDays,
    IReadOnlyList<CourtUsageStatistics> Courts,
    IReadOnlyList<PeakSlotStatistics> PeakSlots,
    IReadOnlyList<ProductStatistics> Products);

public sealed record BusinessStatisticsBranch(Guid Id, string Name, string TimeZone, bool IsOwner);

public sealed record BookingStatisticsDay(
    Guid BranchId, DateOnly Date, long BookingCount, long CompletedCount, long CancelledCount,
    decimal BookedHours, decimal UsedHours, decimal Revenue);

public sealed record SalesStatisticsDay(Guid BranchId, DateOnly Date, long OrderCount, decimal Revenue);

public sealed record CourtUsageStatistics(
    Guid BranchId, string CourtName, decimal UsedHours, decimal AvailableHours);

public sealed record PeakSlotStatistics(string Slot, long BookingCount, decimal Revenue);

public sealed record ProductStatistics(
    string ProductName, string Category, decimal Quantity, decimal Revenue, decimal? Cost);
