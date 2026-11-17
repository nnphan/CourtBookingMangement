using System.ComponentModel.DataAnnotations;

namespace CourtBookingManagement.Application.Options;

public sealed class BookingHoldOptions
{
    public const string SectionName = "BookingHold";

    [Range(30, 600)]
    public int DurationSeconds { get; set; } = 300;

    [Range(1, 100)]
    public int MaximumDetails { get; set; } = 10;

    [Range(5, 10)]
    public int LockExpirySeconds { get; set; } = 8;

    [Range(0, 10000)]
    public int LockWaitMilliseconds { get; set; } = 800;

    [Range(1, 1000)]
    public int LockRetryMilliseconds { get; set; } = 100;

    [Range(0, 10)]
    public int LockRetryCount { get; set; } = 3;

    [Range(5, 120)]
    public int ExpirationWorkerIntervalSeconds { get; set; } = 20;

    [Range(1, 1000)]
    public int ExpirationBatchSize { get; set; } = 100;
}
