namespace CourtBookingManagement.Application.Auth.Exceptions;

public sealed class BranchAccessDeniedException()
    : Exception("The current user does not have the required permission for this branch.");
