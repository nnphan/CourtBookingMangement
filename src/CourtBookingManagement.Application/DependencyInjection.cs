
using CourtBookingManagement.Application.Auth.Interfaces;
using CourtBookingManagement.Application.Auth.Services;
using CourtBookingManagement.Application.CourtBookings.Interfaces;
using CourtBookingManagement.Application.CourtBookings.Services;
using CourtBookingManagement.Application.CourtStatus.Interfaces;
using CourtBookingManagement.Application.CourtStatus.Services;
using CourtBookingManagement.Application.Users.Services;
using CourtBookingManagement.Application.Validators;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CourtBookingManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICourtStatusService, CourtStatusService>();
        services.AddScoped<ICourtBookingService, CourtBookingService>();
        services.AddValidatorsFromAssemblyContaining<CreateUserRequestValidator>();
        return services;
    }
}