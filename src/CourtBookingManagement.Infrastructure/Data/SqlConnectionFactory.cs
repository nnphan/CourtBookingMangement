using System.Data;
using CourtBookingManagement.Application.Abstractions.Data;
using Npgsql;

namespace CourtBookingManagement.Infrastructure.Data;

internal sealed class SqlConnectionFactory(string connectionString) : ISqlConnectionFactory
{
    public IDbConnection CreateConnection() => new NpgsqlConnection(connectionString);
}