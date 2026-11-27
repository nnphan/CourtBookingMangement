namespace CourtBookingManagement.Application.Sales.Models.Responses;

public sealed record BusinessStatisticsResponse(
    IReadOnlyList<BookingStatisticsDay> BookingDays,
    IReadOnlyList<SalesStatisticsDay> SalesDays,
    IReadOnlyList<CourtUsageStatistics> Courts,
    IReadOnlyList<PeakSlotStatistics> PeakSlots,
    IReadOnlyList<ProductStatistics> Products,
    IReadOnlyList<BusinessExpense> Expenses);

public sealed record BusinessStatisticsBranch(Guid Id, string Name, string TimeZone, bool IsOwner);

public sealed record BookingStatisticsDay(
    Guid BranchId, DateOnly Date, long BookingCount, long CompletedCount, long CancelledCount,
    decimal BookedHours, decimal UsedHours, decimal Revenue, long DailyBookingCount);

public sealed record SalesStatisticsDay(Guid BranchId, DateOnly Date, long OrderCount, decimal Revenue);

public sealed record CourtUsageStatistics(
    Guid BranchId, string CourtName, decimal UsedHours, decimal AvailableHours);

public sealed record PeakSlotStatistics(string Slot, long BookingCount, decimal Revenue);

public sealed record ProductStatistics(
    Guid BranchId, DateOnly Date, Guid ProductId, string ProductName, string Category, string Unit,
    decimal Quantity, decimal Revenue, decimal? Cost);

public sealed record BookingStatisticsDetail(
    Guid Id, DateOnly Date, string BookingCode, string Court, string Customer,
    TimeOnly StartTime, TimeOnly EndTime, decimal Amount, string PaymentMethod, string Status);

public sealed record SalesStatisticsDetail(
    Guid Id, DateOnly Date, string OrderCode, string Category, string ProductName,
    string Unit, decimal Quantity, decimal UnitPrice, decimal Amount);

public sealed record BusinessExpense(
    Guid Id, Guid BranchId, DateOnly Date, string Category, string Title, decimal Amount, string? Notes);
