using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CourtBookingManagement.Api.Swagger;

public sealed class BookingPriceExamplesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!string.Equals(context.ApiDescription.RelativePath, "api/bookings/calculate-price", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(context.ApiDescription.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (operation.RequestBody?.Content.TryGetValue("application/json", out var requestContent) == true)
        {
            requestContent.Example = new OpenApiObject
            {
                ["branchId"] = new OpenApiString("e376181a-c1ea-4232-b29a-de1a5d222b90"),
                ["bookingDate"] = new OpenApiString("2026-10-15"),
                ["startTime"] = new OpenApiString("16:00:00"),
                ["endTime"] = new OpenApiString("18:00:00")
            };
        }

        if (operation.Responses.TryGetValue("200", out var response)
            && response.Content.TryGetValue("application/json", out var responseContent))
        {
            responseContent.Example = new OpenApiObject
            {
                ["success"] = new OpenApiBoolean(true),
                ["message"] = new OpenApiString("Price calculated successfully."),
                ["data"] = new OpenApiObject
                {
                    ["branchId"] = new OpenApiString("e376181a-c1ea-4232-b29a-de1a5d222b90"),
                    ["bookingDate"] = new OpenApiString("2026-10-15"),
                    ["startTime"] = new OpenApiString("16:00:00"),
                    ["endTime"] = new OpenApiString("18:00:00"),
                    ["durationHours"] = new OpenApiInteger(2),
                    ["subtotalAmount"] = new OpenApiInteger(250000),
                    ["paidAmount"] = new OpenApiInteger(0),
                    ["totalAmount"] = new OpenApiInteger(250000)
                }
            };
        }
    }
}