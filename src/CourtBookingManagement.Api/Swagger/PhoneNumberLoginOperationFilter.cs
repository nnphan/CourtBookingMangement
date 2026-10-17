using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CourtBookingManagement.Api.Swagger;

public sealed class PhoneNumberLoginOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!string.Equals(
                context.ApiDescription.RelativePath,
                "api/auth/login/phonenumber",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        operation.Summary = "Authenticates a user using phone number and password.";
        operation.Description = operation.Summary;
        operation.Security = [];

        if (operation.RequestBody?.Content.TryGetValue("application/json", out var content) == true)
        {
            content.Example = new OpenApiObject
            {
                ["phoneNumber"] = new OpenApiString("0900000000"),
                ["password"] = new OpenApiString("example-password")
            };
        }
    }
}