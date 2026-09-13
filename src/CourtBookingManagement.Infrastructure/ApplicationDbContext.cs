using CourtBookingManagement.Application.Abstractions.Clock;
using CourtBookingManagement.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CourtBookingManagement.Infrastructure;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IDateTimeProvider dateTimeProvider) : DbContext(options), IUnitOfWork
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        _ = dateTimeProvider.UtcNow;
        return await base.SaveChangesAsync(cancellationToken);
    }
}