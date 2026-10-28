using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using CourtBookingManagement.Application.Options;

namespace CourtBookingManagement.Api.Swagger;

public sealed class BookingCreateExamplesOperationFilter(BookingOptions bookingOptions) : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath?.TrimEnd('/');
        if (!string.Equals(context.ApiDescription.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
            || !(string.Equals(path, "api/bookings", StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, "api/bookings/staff", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        operation.Summary = string.Equals(path, "api/bookings/staff", StringComparison.OrdinalIgnoreCase)
            ? "Create a booking for a customer as authorized staff"
            : "Create a booking with one or more court reservations";
        operation.Description = string.Equals(path, "api/bookings/staff", StringComparison.OrdinalIgnoreCase)
            ? $"Creates one booking with 1 to {bookingOptions.MaximumDetailsPerBooking} reservation details atomically. Provide an existing customerId or guest contact details. All prices are calculated by the server. Requires the booking.create permission."
            : $"Creates one booking with 1 to {bookingOptions.MaximumDetailsPerBooking} reservation details atomically. All prices are calculated by the server. Requires an authenticated customer with an active customer profile. Returns 409 if any selected court is no longer available.";

        if (operation.RequestBody is null
            || !operation.RequestBody.Content.TryGetValue("application/json", out var requestContent))
        {
            return;
        }

        var example = new OpenApiObject
        {
            ["branchId"] = new OpenApiString("e376181a-c1ea-4232-b29a-de1a5d222b90"),
            ["bookingType"] = new OpenApiString("FIXED"),
            ["paymentMethod"] = new OpenApiString("CASH"),
            ["notes"] = new OpenApiString(""),
            ["details"] = new OpenApiArray
            {
                new OpenApiObject
                {
                    ["courtId"] = new OpenApiString("019a2e4b-0000-7000-8000-000000000001"),
                    ["bookingDate"] = new OpenApiString("2026-10-30"),
                    ["startTime"] = new OpenApiString("18:00:00"),
                    ["endTime"] = new OpenApiString("20:00:00")
                },
                new OpenApiObject
                {
                    ["courtId"] = new OpenApiString("019a2e4b-0000-7000-8000-000000000002"),
                    ["bookingDate"] = new OpenApiString("2026-10-30"),
                    ["startTime"] = new OpenApiString("18:00:00"),
                    ["endTime"] = new OpenApiString("20:00:00")
                }
            }
        };

        if (string.Equals(path, "api/bookings/staff", StringComparison.OrdinalIgnoreCase))
        {
            example["customerId"] = new OpenApiString("019a2e4b-0000-7000-8000-000000000003");
        }

        requestContent.Example = example;
    }
}
