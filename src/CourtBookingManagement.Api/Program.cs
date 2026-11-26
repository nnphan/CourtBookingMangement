using CourtBookingManagement.Api.Common.Middleware;
using CourtBookingManagement.Api.Common.Responses;
using CourtBookingManagement.Api.Common.Constants;
using CourtBookingManagement.Api.Files;
using CourtBookingManagement.Api.Swagger;
using CourtBookingManagement.Api.Realtime;
using CourtBookingManagement.Application;
using CourtBookingManagement.Application.Bookings.DTOs;
using CourtBookingManagement.Application.Options;
using CourtBookingManagement.Infrastructure;
using CourtBookingManagement.Infrastructure.Auth;
using CourtBookingManagement.Infrastructure.Repositories;
using Dapper;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configure Dapper
DefaultTypeMap.MatchNamesWithUnderscores = true;
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddProblemDetails();
builder.Services.AddSignalR();
var dataProtection = builder.Services.AddDataProtection();
if (builder.Configuration["DataProtection:KeyDirectory"] is { Length: > 0 } keyDirectory)
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
builder.Services.AddScoped<CourtSelectionRepository>();
builder.Services.AddScoped<CourtSelectionIdentity>();
builder.Services.AddSingleton<CourtSelectionPublisher>();
builder.Services.AddHostedService<CourtSelectionWorker>();
builder.Services.AddHostedService<PlayerMatchConsistencyWorker>();
builder.Services.AddOptions<CourtSelectionOptions>().BindConfiguration("BookingHolds")
    .Validate(o => o.SelectionHoldMinutes > 0 && o.CleanupIntervalSeconds > 0
        && o.MaximumSlotsPerHold > 0 && o.MaximumActiveHoldsPerUser > 0 && o.IdempotencyRetentionMinutes >= 10)
    .ValidateOnStart();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .SelectMany(entry => entry.Value!.Errors.Select(error =>
                    new ValidationError(
                        entry.Key,
                        string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "The supplied value is invalid."
                            : error.ErrorMessage)))
                .ToArray();

            return new BadRequestObjectResult(new ApiErrorResponse(
                false,
                "COMMON_001",
                "One or more validation errors occurred.",
                context.HttpContext.TraceIdentifier,
                DateTimeOffset.UtcNow,
                errors));
        };
    });
var bookingOptions = builder.Configuration.GetSection(BookingOptions.SectionName).Get<BookingOptions>() ?? new BookingOptions();
System.ComponentModel.DataAnnotations.Validator.ValidateObject(
    bookingOptions,
    new System.ComponentModel.DataAnnotations.ValidationContext(bookingOptions),
    validateAllProperties: true);
builder.Services.AddSingleton(bookingOptions);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(
                System.Threading.RateLimiting.MetadataName.RetryAfter,
                out var retryAfter))
        {
            context.HttpContext.Response.Headers["Retry-After"] =
                Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    };
    options.AddPolicy("staff-activation", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("court-selection", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 40,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("matching-write", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("staff-resend", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("dev-smtp-smoke", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:5173", "https://localhost:5173"];
        policy
            .WithOrigins(origins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

builder.Services.AddSwaggerGen(options =>
{
    options.SchemaFilter<BookingEnumSchemaFilter>();
    options.OperationFilter<UploadFileExamplesOperationFilter>();
    options.OperationFilter<AdminBranchesExamplesOperationFilter>();
    options.OperationFilter<BranchOwnerStaffOperationFilter>();
    options.OperationFilter<BookingPriceExamplesOperationFilter>();
    options.OperationFilter<PhoneNumberLoginOperationFilter>();
    options.CustomSchemaIds(GetSwaggerSchemaId);

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] then your token."
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
if (!string.IsNullOrWhiteSpace(jwtOptions?.SecretKey))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        Permissions.BranchCreate,
        policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(Permissions.BranchCreate)));
    options.AddPolicy(
        Permissions.BranchUpdate,
        policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(Permissions.BranchUpdate)));
    options.AddPolicy(
        Permissions.BranchView,
        policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(Permissions.BranchView)));
    
    //options.AddPolicy(
    //    Permissions.FileUpload,
    //    policy => policy.RequireAuthenticatedUser()
    //        .AddRequirements(new PermissionRequirement(Permissions.FileUpload)));
});
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

var databaseConnectionString = builder.Configuration[$"{DatabaseOptions.SectionName}:ConnectionString"];
if (!string.IsNullOrWhiteSpace(databaseConnectionString))
{
    builder.Services.AddHealthChecks()
        .AddNpgSql(
            databaseConnectionString,
            name: "postgresql",
            failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
            tags: ["db", "sql", "postgresql", "ready"]);
}

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("CorsPolicy");
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
    };
});

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHub<CourtStatusHub>("/hubs/court-status").AllowAnonymous();
app.MapHealthChecks("health", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
app.MapHealthChecks("health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();

static string GetSwaggerSchemaId(Type type)
{
    if (type.IsArray)
    {
        return $"{GetSwaggerSchemaId(type.GetElementType()!)}Array";
    }

    if (!type.IsGenericType)
    {
        return (type.FullName ?? type.Name).Replace('+', '.');
    }

    var genericName = type.GetGenericTypeDefinition().FullName ?? type.Name;
    var arityIndex = genericName.IndexOf('`');
    if (arityIndex >= 0)
    {
        genericName = genericName[..arityIndex];
    }

    genericName = genericName.Replace('+', '.');
    var argumentNames = string.Join("_", type.GetGenericArguments().Select(GetSwaggerSchemaId));
    return $"{genericName}_{argumentNames}";
}

public partial class Program;
