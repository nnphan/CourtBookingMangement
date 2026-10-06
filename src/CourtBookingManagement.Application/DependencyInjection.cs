
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Auth.Services;
using CourtBookingManagement.Application.Abstractions.Behaviors;
using CourtBookingManagement.Application.CourtBookings.Interfaces;
using CourtBookingManagement.Application.CourtBookings.Services;
using CourtBookingManagement.Application.Branches.Services;
using CourtBookingManagement.Application.CourtStatus.Interfaces;
using CourtBookingManagement.Application.CourtStatus.Services;
using CourtBookingManagement.Application.Matching.Services;
using CourtBookingManagement.Application.Users.Services;
using CourtBookingManagement.Application.Validators;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CourtBookingManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICourtStatusService, CourtStatusService>();
        services.AddScoped<ICourtBookingService, CourtBookingService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IPlayerMatchService, PlayerMatchService>();
        services.AddScoped<IMyMatchService, MyMatchService>();
        services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>();
        return services;
    }
}