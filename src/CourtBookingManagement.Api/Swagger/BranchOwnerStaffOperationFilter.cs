using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Api.Controllers;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Application.StaffManagement.DTOs;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CourtBookingManagement.Api.Swagger;

public sealed class BranchOwnerStaffOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.DeclaringType != typeof(BranchOwnerStaffController))
        {
            return;
        }

        if (context.MethodInfo.Name is nameof(BranchOwnerStaffController.Create) or nameof(BranchOwnerStaffController.SendActivation))
        {
            operation.Summary = context.MethodInfo.Name == nameof(BranchOwnerStaffController.Create)
                ? "Create Staff in owner scope" : "Resend Staff activation";
            operation.Description = "Requires an active owner and staff.create on the owned branches. " +
                "Only the STAFF branch role is assigned. Invitations are delivered after commit.";
            return;
        }

        operation.Description = "Requires authentication, an active owner profile, and staff.view " +
            "on the relevant active owned branches. Staff with protected roles are excluded.";

        if (context.MethodInfo.Name == nameof(BranchOwnerStaffController.GetBranches))
        {
            operation.Summary = "List owned branches for Staff management";
            operation.Description += " Each branch includes whether the caller has staff.create there.";
            return;
        }

        if (context.MethodInfo.Name != nameof(BranchOwnerStaffController.GetList))
        {
            operation.Summary = "Get Staff detail in owner scope";
            return;
        }

        operation.Summary = "List Staff in owner scope";
        operation.Description += " Results include active assignments only; a foreign branch filter is forbidden.";

        foreach (var parameter in operation.Parameters)
        {
            parameter.Description = parameter.Name.ToLowerInvariant() switch
            {
                "branchid" => "Optional active owned branch ID.",
                "search" => "Display name, email, phone number, or staff code; maximum 100 characters.",
                "status" => "ACTIVE or INACTIVE.",
                "activationstatus" => "PENDING_ACTIVATION, ACTIVE, INACTIVE, or EXPIRED.",
                "sortby" => "displayName, email, staffCode, status, createdAt, or lastLoginAt; default createdAt.",
                "sortdirection" => "ASC or DESC; default DESC.",
                "page" => "One-based page number; default 1.",
                "pagesize" => "Page size from 10 to 100; default 20.",
                _ => parameter.Description
            };
        }

        var responseSchema = context.SchemaGenerator.GenerateSchema(
            typeof(ApiResponse<IReadOnlyList<StaffListItemResponse>>),
            context.SchemaRepository);
        var schema = responseSchema.Reference is null
            ? responseSchema
            : context.SchemaRepository.Schemas[responseSchema.Reference.Id];
        schema.Properties["metadata"] = context.SchemaGenerator.GenerateSchema(
            typeof(PagedResultMetadata),
            context.SchemaRepository);
    }
}
