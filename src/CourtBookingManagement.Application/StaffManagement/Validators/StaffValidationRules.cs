namespace CourtBookingManagement.Application.StaffManagement.Validators;

internal static class StaffValidationRules
{
    public static bool DisplayName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length is >= 2 and <= 100;

    public static bool PhoneNumber(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 20;

    public static bool StaffCode(string? value) =>
        value is null || (!string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 50);

    public static bool JobTitle(string? value) =>
        value is null || (!string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 100);

    public static bool Branches(Guid[]? ids) =>
        ids is { Length: > 0 } && ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Length;
}
