namespace CourtBookingManagement.Application.Common.Constants;

public static class SkillLevels
{
    public const string Beginner = "BEGINNER";

    public const string Intermediate = "INTERMEDIATE";

    public const string Advanced = "ADVANCED";

    public const string Professional = "PROFESSIONAL";

    public static string FromRating(int rating) => rating switch
    {
        < 400 => Beginner,
        < 600 => Intermediate,
        < 900 => Advanced,
        _ => Professional
    };

    public static readonly string[] All =
    [
        Beginner,
        Intermediate,
        Advanced,
        Professional
    ];
}
