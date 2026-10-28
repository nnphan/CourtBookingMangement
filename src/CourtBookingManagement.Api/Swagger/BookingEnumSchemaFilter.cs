using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using CourtBookingManagement.Application.Common.Constants;
using CourtBookingManagement.Application.Common.Enums;
using CourtBookingManagement.Application.Options;
using CourtBookingManagement.Application.CourtBookings.DTOs;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Application.CourtStatus.DTOs;

namespace CourtBookingManagement.Api.Swagger;

public sealed class BookingEnumSchemaFilter : ISchemaFilter
{
    private readonly BookingOptions _bookingOptions;

    public BookingEnumSchemaFilter(BookingOptions bookingOptions)
    {
        _bookingOptions = bookingOptions;
    }

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        switch (context.Type.Name)
        {
            case nameof(CreateCourtBookingRequest):
            case nameof(CreateBookingRequest):
            case nameof(UpdateBookingRequest):
                ApplyValues(schema, "BookingType", BookingTypes.All, BookingTypes.Fixed);
                break;
        }

        if (context.Type == typeof(CreateCourtBookingRequest)
            || context.Type == typeof(CourtBookingManagement.Application.Bookings.DTOs.CreateBookingRequest)
            || context.Type == typeof(CourtBookingManagement.Application.Bookings.DTOs.CreateStaffBookingRequest))
        {
            ApplyValues(schema, "PaymentMethod", PaymentMethods.All, PaymentMethods.Cash);
            ApplyValues(schema, "BookingType", BookingTypes.All, BookingTypes.Fixed);
        }
        else if (context.Type == typeof(CreateCourtBookingResponse))
        {
            ApplyValues(schema, "BookingType", BookingTypes.All, "Flexible");
            ApplyValues(schema, "PaymentMethod", PaymentMethods.All, "BankTransfer");
        }
        else if (context.Type == typeof(BookingDetailResponse))
        {
            ApplyValues(schema, "BookingType", BookingTypes.All, "Flexible");
            ApplyValues(schema, "PaymentMethod", PaymentMethods.All, "BankTransfer");
        }

        if (context.Type == typeof(CreatePlayerMatchRequest))
        {
            ApplyValues(schema, "SkillLevel", SkillLevels.All, SkillLevels.Beginner);
            var skillLevelProperty = schema.Properties.FirstOrDefault(property =>
                string.Equals(property.Key, "SkillLevel", StringComparison.OrdinalIgnoreCase)).Value;
            if (skillLevelProperty is not null)
            {
                skillLevelProperty.Description =
                    "Allowed values: BEGINNER, INTERMEDIATE, ADVANCED, PROFESSIONAL.";
            }
        }

        if (context.Type == typeof(CreatePlayerMatchRequest)
            || context.Type == typeof(PlayerMatchSearchRequest)
            || context.Type == typeof(PlayerMatchDetailResponse)
            || context.Type == typeof(PlayerMatchResponse)
            || context.Type == typeof(MyMatchResponse))
        {
            ApplyValues(schema, "GenderPreference", GenderPreferences.All, GenderPreferences.Both);
            var genderPreferenceProperty = schema.Properties.FirstOrDefault(property =>
                string.Equals(property.Key, "GenderPreference", StringComparison.OrdinalIgnoreCase)).Value;
            if (genderPreferenceProperty is not null)
            {
                genderPreferenceProperty.Description = "Allowed values: MALE, FEMALE, BOTH.";
            }
        }

        if (context.Type == typeof(CourtBookingManagement.Application.Bookings.DTOs.CreateBookingDetailRequest))
        {
            AddRequired(schema, "courtId", "bookingDate", "startTime", "endTime");
            SetDescription(schema, "BookingDate", "Business-local date in YYYY-MM-DD format.");
            SetDescription(schema, "StartTime", "Business-local time in HH:mm or HH:mm:ss format.");
            SetDescription(schema, "EndTime", "Business-local time in HH:mm or HH:mm:ss format; must be later than startTime.");
        }

        if (context.Type == typeof(CourtBookingManagement.Application.Bookings.DTOs.CreateBookingRequest))
        {
            AddRequired(schema, "branchId", "bookingType", "paymentMethod", "details");
            SetDescription(
                schema,
                "Details",
                $"Provide between 1 and {_bookingOptions.MaximumDetailsPerBooking} details. All are created or rejected atomically; prices are calculated by the server.");
            SetDetailBounds(schema, "details", _bookingOptions.MaximumDetailsPerBooking);
        }

        if (context.Type == typeof(CourtBookingManagement.Application.Bookings.DTOs.CreateStaffBookingRequest))
        {
            AddRequired(schema, "branchId", "bookingType", "paymentMethod", "details");
            SetDescription(
                schema,
                "Details",
                $"Provide between 1 and {_bookingOptions.MaximumDetailsPerBooking} details. All are created or rejected atomically; prices are calculated by the server.");
            SetDetailBounds(schema, "details", _bookingOptions.MaximumDetailsPerBooking);
        }
    }

    private static void ApplyValues(OpenApiSchema schema, string propertyName, IEnumerable<string> values, string example)
    {
        var property = schema.Properties.FirstOrDefault(entry =>
            string.Equals(entry.Key, propertyName, StringComparison.OrdinalIgnoreCase)).Value;
        if (property is null) return;

        property.Enum = values.Select(value => (IOpenApiAny)new OpenApiString(value)).ToList();
        property.Example = new OpenApiString(example);
    }

    private static void SetDescription(OpenApiSchema schema, string propertyName, string description)
    {
        var property = schema.Properties.FirstOrDefault(entry =>
            string.Equals(entry.Key, propertyName, StringComparison.OrdinalIgnoreCase)).Value;
        if (property is not null)
        {
            property.Description = description;
        }
    }

    private static void AddRequired(OpenApiSchema schema, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var actualName = schema.Properties.Keys.FirstOrDefault(name =>
                string.Equals(name, propertyName, StringComparison.OrdinalIgnoreCase));
            if (actualName is not null)
            {
                schema.Required.Add(actualName);
                schema.Properties[actualName].Nullable = false;
            }
        }
    }

    private static void SetDetailBounds(OpenApiSchema schema, string propertyName, int maximum)
    {
        var details = schema.Properties.FirstOrDefault(entry =>
            string.Equals(entry.Key, propertyName, StringComparison.OrdinalIgnoreCase)).Value;
        if (details is not null)
        {
            details.MinItems = 1;
            details.MaxItems = maximum;
        }
    }
}