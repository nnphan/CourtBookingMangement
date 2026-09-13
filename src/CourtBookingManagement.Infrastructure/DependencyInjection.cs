using CourtBookingManagement.Application.Abstractions.Clock;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Options;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Infrastructure.Clock;
using CourtBookingManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CourtBookingManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        var connectionString = configuration.GetConnectionString("Database")
            ?? configuration[$"{DatabaseOptions.SectionName}:ConnectionString"]
            ?? throw new InvalidOperationException("The database connection string is not configured.");

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.MinPoolSize <= options.MaxPoolSize,
                "Database minimum pool size cannot exceed maximum pool size")
            .ValidateOnStart();

        services.AddDbContextPool<ApplicationDbContext>((serviceProvider, options) =>
        {
            var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            var databaseConnectionString = ConfigureConnectionPooling(databaseOptions);

            options
                .UseNpgsql(databaseConnectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: databaseOptions.RetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(databaseOptions.MaxRetryDelaySeconds),
                        errorCodesToAdd: null);
                })
                .UseSnakeCaseNamingConvention();
        }, poolSize: 128);

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<ISqlConnectionFactory>(serviceProvider =>
        {
            var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            return new SqlConnectionFactory(ConfigureConnectionPooling(databaseOptions));
        });

        return services;
    }

    private static string ConfigureConnectionPooling(DatabaseOptions options)
    {
        var builder = new NpgsqlConnectionStringBuilder(options.ConnectionString)
        {
            Pooling = true,
            MinPoolSize = options.MinPoolSize,
            MaxPoolSize = options.MaxPoolSize,
            ConnectionIdleLifetime = 300,
            ConnectionPruningInterval = 10,
            Timeout = options.ConnectionTimeout,
            CommandTimeout = options.CommandTimeout
        };

        return builder.ConnectionString;
    }
}