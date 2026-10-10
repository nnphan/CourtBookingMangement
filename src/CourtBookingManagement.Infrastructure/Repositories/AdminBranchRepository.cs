using System.Diagnostics;
using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.Admin.Branches.DTOs;
using CourtBookingManagement.Application.Admin.Branches.Interfaces;
using CourtBookingManagement.Application.Matching.Models;
using CourtBookingManagement.Infrastructure.SqlBuilders;
using Dapper;
using Microsoft.Extensions.Logging;

namespace CourtBookingManagement.Infrastructure.Repositories;

public sealed class AdminBranchRepository(
    ISqlConnectionFactory sqlConnectionFactory,
    ILogger<AdminBranchRepository> logger) : IAdminBranchRepository
{
    public async Task<BranchSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var connection = sqlConnectionFactory.CreateConnection();
            var summary = await connection.QuerySingleAsync<BranchSummaryResponse>(new CommandDefinition(
                AdminBranchSqlBuilder.BuildGetSummary(),
                cancellationToken: cancellationToken));

            logger.LogInformation(
                "Admin branch summary completed in {ExecutionTimeMs} ms. BranchCount: {BranchCount}, CourtCount: {CourtCount}",
                stopwatch.Elapsed.TotalMilliseconds,
                summary.TotalBranches,
                summary.TotalCourts);
            return summary;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Admin branch summary query failed after {ExecutionTimeMs} ms", stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    public async Task<PagedResult<BranchListResponse>> GetBranchesAsync(
        GetBranchesRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation(
            "Searching admin branches. Keyword: {Keyword}, City: {City}, District: {District}, IsActive: {IsActive}, PageNumber: {PageNumber}, PageSize: {PageSize}",
            request.Keyword,
            request.City,
            request.District,
            request.IsActive,
            request.PageNumber,
            request.PageSize);

        try
        {
            var (sql, parameters) = AdminBranchSqlBuilder.BuildGetBranches(request);
            using var connection = sqlConnectionFactory.CreateConnection();
            using var results = await connection.QueryMultipleAsync(new CommandDefinition(
                sql,
                parameters,
                cancellationToken: cancellationToken));

            var items = (await results.ReadAsync<BranchListResponse>()).AsList();
            var totalCount = await results.ReadSingleAsync<long>();
            var page = PagedResult<BranchListResponse>.Create(
                items,
                request.PageNumber,
                request.PageSize,
                totalCount);

            logger.LogInformation(
                "Admin branch query completed in {ExecutionTimeMs} ms. RecordCount: {RecordCount}, TotalCount: {TotalCount}",
                stopwatch.Elapsed.TotalMilliseconds,
                items.Count,
                totalCount);
            return page;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Admin branch query failed after {ExecutionTimeMs} ms", stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }
}