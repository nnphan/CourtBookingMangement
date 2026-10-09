using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CourtBookingManagement.Api.Files;

public sealed class UploadFileExamplesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!string.Equals(context.ApiDescription.RelativePath, "api/files/upload", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(context.ApiDescription.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (operation.RequestBody?.Content.TryGetValue("multipart/form-data", out var formContent) == true)
        {
            if (formContent.Schema.Properties.TryGetValue("category", out var categorySchema))
            {
                categorySchema.Enum = [
                    new OpenApiString("branch"),
                    new OpenApiString("court"),
                    new OpenApiString("avatar"),
                    new OpenApiString("tournament"),
                    new OpenApiString("promotion")
                ];
            }

            formContent.Example = new OpenApiObject
            {
                ["file"] = new OpenApiString("arena.jpg"),
                ["category"] = new OpenApiString("branch")
            };
        }

        if (operation.Responses.TryGetValue("200", out var response)
            && response.Content.TryGetValue("application/json", out var responseContent))
        {
            responseContent.Example = new OpenApiObject
            {
                ["success"] = new OpenApiBoolean(true),
                ["message"] = new OpenApiString("File uploaded successfully"),
                ["data"] = new OpenApiObject
                {
                    ["fileName"] = new OpenApiString("a18c7a3f-6417-4fd4.jpg"),
                    ["url"] = new OpenApiString("https://res.cloudinary.com/example/image/upload/a18c7a3f-6417-4fd4.jpg"),
                    ["contentType"] = new OpenApiString("image/jpeg"),
                    ["size"] = new OpenApiLong(123456),
                    ["category"] = new OpenApiString("branch")
                }
            };
        }
    }
}