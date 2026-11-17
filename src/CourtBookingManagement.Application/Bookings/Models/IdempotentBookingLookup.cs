using CourtBookingManagement.Application.Bookings.DTOs;

namespace CourtBookingManagement.Application.Bookings.Models;

public sealed record IdempotentBookingLookup(
    bool KeyReused,
    CreateBookingResponse? Booking);
