namespace CourtBookingManagement.Application.Matching.Models;

public enum SkillLevel
{
    Beginner = 1,
    Intermediate = 2,
    Advanced = 3,
    Pro = 4
}

public static class SkillLevelExtensions
{
    public static string ToDatabaseValue(this SkillLevel skillLevel) => skillLevel switch
    {
        SkillLevel.Beginner => "Beginner",
        SkillLevel.Intermediate => "Intermediate",
        SkillLevel.Advanced => "Advanced",
        SkillLevel.Pro => "Pro",
        _ => throw new ArgumentOutOfRangeException(nameof(skillLevel), skillLevel, "Unsupported skill level.")
    };
}
