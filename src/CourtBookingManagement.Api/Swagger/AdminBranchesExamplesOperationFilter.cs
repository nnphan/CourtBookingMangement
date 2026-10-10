using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Application.Admin.Branches.DTOs;
using CourtBookingManagement.Application.Matching.Models;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CourtBookingManagement.Api.Swagger;

public sealed class AdminBranchesExamplesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath;
        if (string.Equals(path, "api/admin/branches", StringComparison.OrdinalIgnoreCase)
            && string.Equals(context.ApiDescription.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            AddMetadataSchema(context);
            SetResponseExample(operation, new OpenApiObject
            {
                ["success"] = new OpenApiBoolean(true),
                ["message"] = new OpenApiString("Success"),
                ["data"] = new OpenApiArray
                {
                    new OpenApiObject
                    {
                        ["id"] = new OpenApiString("d3b43956-305e-47db-ad84-c24f33c68767"),
                        ["name"] = new OpenApiString("ALOBO Arena Quan 7"),
                        ["city"] = new OpenApiString("Ho Chi Minh"),
                        ["district"] = new OpenApiString("Quan 7"),
                        ["phoneNumber"] = new OpenApiString("0971899081"),
                        ["totalCourts"] = new OpenApiInteger(12),
                        ["isActive"] = new OpenApiBoolean(true),
                        ["createdAt"] = new OpenApiString("2026-10-09T10:00:00Z")
                    }
                },
                ["metadata"] = new OpenApiObject
                {
                    ["pageNumber"] = new OpenApiInteger(1),
                    ["pageSize"] = new OpenApiInteger(10),
                    ["totalCount"] = new OpenApiLong(53),
                    ["totalPages"] = new OpenApiInteger(6),
                    ["hasPreviousPage"] = new OpenApiBoolean(false),
                    ["hasNextPage"] = new OpenApiBoolean(true)
                }
            });
        }
        else if (string.Equals(path, "api/admin/branches/summary", StringComparison.OrdinalIgnoreCase)
                 && string.Equals(context.ApiDescription.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            SetResponseExample(operation, new OpenApiObject
            {
                ["success"] = new OpenApiBoolean(true),
                ["message"] = new OpenApiString("Success"),
                ["data"] = new OpenApiObject
                {
                    ["totalBranches"] = new OpenApiInteger(53),
                    ["activeBranches"] = new OpenApiInteger(46),
                    ["inactiveBranches"] = new OpenApiInteger(7),
                    ["totalCourts"] = new OpenApiInteger(670)
                }
            });
        }
    }

    private static void AddMetadataSchema(OperationFilterContext context)
    {
        var responseSchema = context.SchemaGenerator.GenerateSchema(
            typeof(ApiResponse<IReadOnlyList<BranchListResponse>>),
            context.SchemaRepository);
        var schema = responseSchema.Reference is null
            ? responseSchema
            : context.SchemaRepository.Schemas[responseSchema.Reference.Id];
        schema.Properties["metadata"] = context.SchemaGenerator.GenerateSchema(
            typeof(PagedResultMetadata),
            context.SchemaRepository);
    }

    private static void SetResponseExample(OpenApiOperation operation, IOpenApiAny example)
    {
        if (operation.Responses.TryGetValue("200", out var response)
            && response.Content.TryGetValue("application/json", out var content))
        {
            content.Example = example;
        }
    }
}