namespace CourtBookingManagement.Application.Branches.Services;

public sealed class BranchAlreadyExistsException() : Exception("Branch already exists.");

public sealed class InvalidBranchAmenitiesException() : Exception("One or more amenities do not exist.");

public sealed class BranchOwnerNotFoundException() : Exception("An active branch owner account is required.");