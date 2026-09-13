using System.ComponentModel.DataAnnotations;

namespace CourtBookingManagement.Application.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required(ErrorMessage = "Database connection string is required")]
    public required string ConnectionString { get; set; }

    [Range(1, 1000, ErrorMessage = "Database maximum pool size must be between 1 and 1000")]
    public int MaxPoolSize { get; set; } = 100;

    [Range(0, 1000, ErrorMessage = "Database minimum pool size must be between 0 and 1000")]
    public int MinPoolSize { get; set; } = 1;

    [Range(1, 300, ErrorMessage = "Database connection timeout must be between 1 and 300 seconds")]
    public int ConnectionTimeout { get; set; } = 30;

    [Range(1, 600, ErrorMessage = "Database command timeout must be between 1 and 600 seconds")]
    public int CommandTimeout { get; set; } = 60;

    [Range(0, 10, ErrorMessage = "Database retry count must be between 0 and 10")]
    public int RetryCount { get; set; } = 3;

    [Range(1, 60, ErrorMessage = "Database maximum retry delay must be between 1 and 60 seconds")]
    public int MaxRetryDelaySeconds { get; set; } = 5;
}