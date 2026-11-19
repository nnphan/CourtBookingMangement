using CourtBookingManagement.Application.Abstractions.Clock;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Bookings.Interfaces;
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Admin.Branches.Interfaces;
using CourtBookingManagement.Application.Branches.Interfaces;
using CourtBookingManagement.Application.Amenities.Repositories;
using CourtBookingManagement.Application.Courts.Repositories;
using CourtBookingManagement.Application.Catalog.Repositories;
using CourtBookingManagement.Application.Inventory.Repositories;
using CourtBookingManagement.Application.Sales.Repositories;
using CourtBookingManagement.Application.Sales.Services;
using CourtBookingManagement.Application.Bookings.Services;
using CourtBookingManagement.Application.Customers.Repositories;
using CourtBookingManagement.Application.CourtStatus.Interfaces;
using CourtBookingManagement.Application.CourtBookings.Interfaces;
using CourtBookingManagement.Application.Matching.Repositories;
using CourtBookingManagement.Application.Matching.Options;
using CourtBookingManagement.Application.Notifications.Repositories;
using CourtBookingManagement.Application.Roles.Repositories;
using CourtBookingManagement.Application.Permissions.Repositories;
using CourtBookingManagement.Application.UserRoles.Repositories;
using CourtBookingManagement.Application.Options;
using CourtBookingManagement.Application.Payments.Interfaces;
using CourtBookingManagement.Application.Payments.Services;
using CourtBookingManagement.Domain.Abstractions;
using CourtBookingManagement.Domain.Users;
using CourtBookingManagement.Infrastructure.Auth;
using CourtBookingManagement.Infrastructure.BackgroundServices;
using CourtBookingManagement.Infrastructure.Clock;
using CourtBookingManagement.Infrastructure.Data;
using CourtBookingManagement.Infrastructure.FileStorage;
using CourtBookingManagement.Infrastructure.Persistence;
using CourtBookingManagement.Infrastructure.Persistence.DapperHandlers;
using CourtBookingManagement.Infrastructure.Redis;
using CourtBookingManagement.Infrastructure.Repositories;
using CourtBookingManagement.Infrastructure.Sales;
using CourtBookingManagement.Infrastructure.Momo;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;

namespace CourtBookingManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddHttpContextAccessor();

        var connectionString = configuration.GetConnectionString("Database")
            ?? configuration[$"{DatabaseOptions.SectionName}:ConnectionString"]
            ?? throw new InvalidOperationException("The database connection string is not configured.");

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.MinPoolSize <= options.MaxPoolSize,
                "Database minimum pool size cannot exceed maximum pool size")
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<BookingHoldOptions>()
            .Bind(configuration.GetSection(BookingHoldOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<PlayerMatchScoringOptions>()
            .Bind(configuration.GetSection(PlayerMatchScoringOptions.SectionName))
            .Validate(options =>
                options.MinimumSkillTolerance > 0
                && options.MinimumMatchScore is >= 0 and <= 100
                && options.MinimumSkillScore is >= 0 and <= 100
                && options.MinimumTimeScore is >= 0 and <= 100,
                "Player-match scoring tolerance and quality gates must be within valid ranges.")
            .ValidateOnStart();

        services.AddOptions<MomoOptions>()
            .Bind(configuration.GetSection(MomoOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SePayOptions>()
            .Bind(configuration.GetSection(SePayOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => !options.HasAnyPaymentConfiguration || options.IsValidConfiguration(),
                "SePay configuration is incomplete or invalid.")
            .ValidateOnStart();

        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CloudinarySettings>()
            .Bind(configuration.GetSection(CloudinarySettings.SectionName))
            .ValidateDataAnnotations();

        services.AddSingleton<ICloudinaryFileUploadClient>(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<CloudinarySettings>>().Value;
            var account = new CloudinaryDotNet.Account(
                settings.CloudName,
                settings.ApiKey,
                settings.ApiSecret);
            return new CloudinaryFileUploadClient(new CloudinaryDotNet.Cloudinary(account));
        });
        services.AddScoped<IMediaFolderResolver, MediaFolderResolver>();
        services.AddScoped<IFileStorageService, CloudinaryFileStorageService>();

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

        // Register Dapper Type Handlers here so API doesn't need to know about Dapper
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new PaymentMethodTypeHandler());

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IBranchQueryRepository, BranchQueryRepository>();
        services.AddScoped<IAdminBranchRepository, AdminBranchRepository>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<AmenityRepository>();
        services.AddScoped<IAmenityRepository>(provider => provider.GetRequiredService<AmenityRepository>());
        services.AddScoped<IAmenityListRepository>(provider => provider.GetRequiredService<AmenityRepository>());
        services.AddScoped<ICourtStatusRepository, CourtStatusRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingExpirationRepository, BookingExpirationRepository>();
        services.AddScoped<BookingExpirationService>();
        services.AddHostedService<BookingHoldExpirationWorker>();
        services.AddHttpClient<IMomoClient, MomoClient>((serviceProvider, client) =>
        {
            var momoOptions = serviceProvider.GetRequiredService<IOptions<MomoOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(momoOptions.RequestTimeoutSeconds);
        });
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ISePayPaymentService, SePayPaymentService>();
        services.AddScoped<ISePayWebhookService, SePayWebhookService>();
        services.AddSingleton<ISePayPaymentCodeGenerator, SePayPaymentCodeGenerator>();
        services.AddSingleton<ISePayQrUrlBuilder, SePayQrUrlBuilder>();
        services.AddSingleton<ISePayWebhookAuthenticator, SePayWebhookAuthenticator>();
        services.AddScoped<IMomoIpnService, MomoIpnService>();
        services.AddScoped<IMomoSignatureService, MomoSignatureService>();
        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var redisOptions = serviceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;
            var redisConfiguration = ConfigurationOptions.Parse(redisOptions.Configuration);
            redisConfiguration.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisConfiguration);
        });
        services.AddSingleton(serviceProvider =>
        {
            var redisOptions = serviceProvider.GetRequiredService<IOptions<RedisOptions>>().Value;
            var redisConfiguration = ConfigurationOptions.Parse(redisOptions.Configuration);
            redisConfiguration.AbortOnConnectFail = false;
            var endpoints = redisConfiguration.EndPoints
                .Select(endpoint => new RedLockEndPoint
                {
                    EndPoint = endpoint,
                    Password = redisConfiguration.Password,
                    Ssl = redisConfiguration.Ssl
                })
                .ToArray();
            return RedLockFactory.Create(endpoints);
        });
        services.AddSingleton<IBookingSlotLockService, RedlockBookingSlotLockService>();
        services.AddSingleton<IBookingHoldCache, RedisBookingHoldCache>();
        services.AddScoped<ICourtBookingRepository, CourtBookingRepository>();
        services.AddScoped<IPlayerMatchRepository, PlayerMatchRepository>();
        services.AddScoped<IPlayerMatchingProfileRepository, PlayerMatchingProfileRepository>();
        services.AddScoped<IMyMatchRepository, MyMatchRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<ICourtRepository, CourtRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<ISalesRepository, SalesRepository>();
        services.AddSingleton<IInvoicePdfGenerator, PdfInvoiceGenerator>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<CourtBookingManagement.Application.Owners.Repositories.IOwnerRepository, OwnerRepository>();
        services.AddScoped<IMembershipLevelRepository, MembershipLevelRepository>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IBranchAuthorizationService, BranchAuthorizationService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<PermissionAuthorizationHandler>();
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