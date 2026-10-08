using CourtBookingManagement.Application.Branches.Interfaces;
using CourtBookingManagement.Application.Amenities.Repositories;
using CourtBookingManagement.Application.Amenities.Models.Responses;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Infrastructure.Persistence;
using CourtBookingManagement.Infrastructure.Repositories.SqlBuilders;
using System.Data.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class AmenityRepository(
    ApplicationDbContext dbContext,
    ISqlConnectionFactory sqlConnectionFactory,
    ILogger<AmenityRepository> logger) : IAmenityRepository, IAmenityListRepository
{
    public Task<List<Guid>> GetExistingIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var requestedIds = ids.Distinct().ToArray();
        return dbContext.Amenities
            .Where(amenity => requestedIds.Contains(amenity.Id))
            .Select(amenity => amenity.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AmenityResponse>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var sql = AmenitySqlBuilder.BuildGetAll();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        logger.LogInformation("Executing query to retrieve all amenities");

        try
        {
            using var connection = sqlConnectionFactory.CreateConnection();
            if (connection is not DbConnection dbConnection)
            {
                throw new InvalidOperationException("The SQL connection must support asynchronous opening.");
            }

            await dbConnection.OpenAsync(cancellationToken);
            var amenities = (await connection.QueryAsync<AmenityResponse>(
                new CommandDefinition(sql, cancellationToken: cancellationToken))).AsList();

            logger.LogInformation(
                "Retrieved {AmenityCount} amenities in {ElapsedMilliseconds} ms",
                amenities.Count,
                stopwatch.ElapsedMilliseconds);

            return amenities;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "Failed to retrieve amenities in {ElapsedMilliseconds} ms",
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}