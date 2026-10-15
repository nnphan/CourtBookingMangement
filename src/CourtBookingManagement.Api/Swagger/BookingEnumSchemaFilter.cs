using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using CourtBookingManagement.Application.Common.Constants;
using CourtBookingManagement.Application.Common.Enums;
using CourtBookingManagement.Application.CourtBookings.DTOs;
using CourtBookingManagement.Application.Matching.Models.Requests;
using CourtBookingManagement.Application.Matching.Models.Responses;
using CourtBookingManagement.Application.CourtStatus.DTOs;

namespace CourtBookingManagement.Api.Swagger;

public sealed class BookingEnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        switch (context.Type.Name)
        {
            case nameof(CreateCourtBookingRequest):
            case nameof(CreateBookingRequest):
            case nameof(UpdateBookingRequest):
                ApplyValues(schema, "BookingType", BookingTypes.All, "Fixed");
                break;
        }

        if (context.Type == typeof(CreateCourtBookingRequest))
        {
            ApplyValues(schema, "PaymentMethod", PaymentMethods.All, "Momo");
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
    }

    private static void ApplyValues(OpenApiSchema schema, string propertyName, IEnumerable<string> values, string example)
    {
        var property = schema.Properties.FirstOrDefault(entry =>
            string.Equals(entry.Key, propertyName, StringComparison.OrdinalIgnoreCase)).Value;
        if (property is null) return;

        property.Enum = values.Select(value => (IOpenApiAny)new OpenApiString(value)).ToList();
        property.Example = new OpenApiString(example);
    }
}