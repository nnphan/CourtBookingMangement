using CourtBookingManagement.Application.Abstractions.Data;
using CourtBookingManagement.Application.CourtBookings.DTOs;
using CourtBookingManagement.Application.CourtBookings.Interfaces;
using Dapper;


public sealed class CourtBookingRepository(ISqlConnectionFactory connectionFactory) : ICourtBookingRepository
{
    public Task<bool> BranchExistsAsync(Guid branchId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM core.branches WHERE id = @BranchId AND is_active AND deleted_at IS NULL);", new { BranchId = branchId }, cancellationToken);

    public Task<bool> CourtExistsAsync(Guid branchId, Guid courtId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM core.courts WHERE id = @CourtId AND branch_id = @BranchId AND is_active AND deleted_at IS NULL);", new { BranchId = branchId, CourtId = courtId }, cancellationToken);

    public Task<bool> CourtExistsAsync(Guid courtId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM core.courts WHERE id = @CourtId AND is_active AND deleted_at IS NULL);", new { CourtId = courtId }, cancellationToken);

    public Task<bool> CourtIsBlockedAsync(Guid courtId, CancellationToken cancellationToken) =>
        ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM core.courts WHERE id = @CourtId AND LOWER(status) IN ('blocked', 'inactive'));", new { CourtId = courtId }, cancellationToken);

    public Task<bool> IsWithinOperatingHoursAsync(Guid branchId, DateOnly date, TimeOnly startTime, TimeOnly endTime, CancellationToken cancellationToken) =>
        ScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM core.operating_hours WHERE branch_id = @BranchId AND NOT is_closed AND @StartTime >= open_time AND @EndTime <= close_time);", new { BranchId = branchId, StartTime = startTime, EndTime = endTime }, cancellationToken);

    public async Task<bool> IsAvailableAsync(Guid courtId, DateOnly date, TimeOnly startTime, TimeOnly endTime, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition("SELECT booking.fn_validate_court_availability(@CourtId, @BookingDate, @StartTime ::time, @EndTime ::time);", new { CourtId = courtId, BookingDate = date, StartTime = startTime.ToString("HH:mm:ss"), EndTime = endTime.ToString("HH:mm:ss") }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(command);
    }

    public async Task<CreateCourtBookingResponse?> CreateGuestBookingAsync(CreateCourtBookingRequest request, CancellationToken cancellationToken)
    {
        const string sql = "SELECT * FROM booking.fn_create_guest_booking(@BranchId, @CourtId, @BookingDate, @StartTime ::time, @EndTime ::time, @CustomerName, @PhoneNumber, @Note, @PaymentMethod);";
        using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new
        {
            request.BranchId,
            request.CourtId,
            request.BookingDate,
            request.StartTime,
            request.EndTime,
            request.CustomerName,
            request.PhoneNumber,
            request.Note,
            request.PaymentMethod
        }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<CreateCourtBookingResponse>(command);
    }

    public async Task<IReadOnlyList<CreateCourtBookingResponse>> SearchAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT b.id AS BookingId, b.booking_code AS BookingCode, b.status AS BookingStatus,
                   c.full_name AS CustomerName, b.total_amount AS TotalAmount, d.booking_date AS BookingDate,
                   br.name AS BranchName, d.start_time AS StartTime, d.end_time AS EndTime,
                   'Cash' AS PaymentMethod, b.created_at AS CreatedAt
            FROM booking.bookings b
            JOIN customer.customers c ON c.id = b.customer_id
            JOIN core.branches br ON br.id = b.branch_id
            JOIN booking.booking_details d ON d.booking_id = b.id
            WHERE c.phone_number = @PhoneNumber AND b.is_active
            ORDER BY d.booking_date DESC, d.start_time DESC;
            """;
        using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new { PhoneNumber = phoneNumber }, cancellationToken: cancellationToken);
        return (await connection.QueryAsync<CreateCourtBookingResponse>(command)).AsList();
    }

    public async Task<bool> CancelAsync(string bookingCode, string phoneNumber, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE booking.bookings b
            SET status = 'cancelled', is_active = false, deleted_at = CURRENT_TIMESTAMP
            FROM customer.customers c
            WHERE b.customer_id = c.id AND b.booking_code = @BookingCode AND c.phone_number = @PhoneNumber
              AND b.is_active AND LOWER(b.status) NOT IN ('completed', 'cancelled')
            RETURNING b.id;
            """;
        using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, new { BookingCode = bookingCode, PhoneNumber = phoneNumber }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Guid?>(command) is not null;
    }

    private async Task<T> ScalarAsync<T>(string sql, object parameters, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }
}