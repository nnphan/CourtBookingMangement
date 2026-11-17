using System.ComponentModel.DataAnnotations;

namespace CourtBookingManagement.Infrastructure.Redis;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    [Required]
    public string Configuration { get; set; } = "localhost:6379,abortConnect=false,connectTimeout=1000";

    public int Database { get; init; }

    public string InstanceName { get; init; } = "badminton:";
}
