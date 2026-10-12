using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using CourtBookingManagement.Application.Common.Enums;
using CourtBookingManagement.Application.CourtBookings.DTOs;
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