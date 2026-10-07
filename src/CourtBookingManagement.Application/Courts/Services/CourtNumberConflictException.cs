namespace CourtBookingManagement.Application.Courts.Services;

public sealed class CourtNumberConflictException() : Exception("Court number already exists in this branch.");
