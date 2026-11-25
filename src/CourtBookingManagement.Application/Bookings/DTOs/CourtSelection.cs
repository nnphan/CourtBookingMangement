namespace CourtBookingManagement.Application.Bookings.DTOs;

public sealed record CourtSelectionRequest(
    Guid BranchId, DateOnly BusinessDate, Guid OwnerSessionId, Guid CourtId,
    TimeOnly StartTime, TimeOnly EndTime, Guid? HoldId, Guid? SlotId);

public sealed record CourtSelectionSlot
{
    public Guid SlotId { get; init; }
    public Guid CourtId { get; init; }
    public string StartTime { get; init; } = "";
    public string EndTime { get; init; } = "";
    public DateTime StartAt { get; init; }
    public DateTime EndAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Guid? HoldId { get; init; }
}

public sealed record CourtSelectionResult(string? Code = null, Guid? HoldId = null, Guid? SlotId = null,
    DateTime? ServerTime = null, DateTime? ExpiresAt = null, CourtSelectionSlot? Slot = null);
public sealed record CourtSelectionOwner(Guid SubjectId, Guid? UserId);
public sealed record CourtSelectionWindow(string StartTime, string EndTime);
public sealed record CourtSelectionBoard(Guid BranchId, DateOnly BusinessDate, DateTime ServerTime,
    string? TimeZone, IReadOnlyList<CourtSelectionWindow> OperatingWindows, IReadOnlyList<CourtSelectionSlot> Slots);
public sealed record CourtSelectionChanged(Guid BranchId, DateOnly BusinessDate);

public sealed class CourtSelectionOptions
{
    public int SelectionHoldMinutes { get; set; } = 5;
    public int CleanupIntervalSeconds { get; set; } = 10;
    public int MaximumSlotsPerHold { get; set; } = 8;
    public int MaximumActiveHoldsPerUser { get; set; } = 1;
    public int IdempotencyRetentionMinutes { get; set; } = 10;
}
