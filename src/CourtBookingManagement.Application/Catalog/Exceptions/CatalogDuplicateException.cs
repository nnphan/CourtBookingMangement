namespace CourtBookingManagement.Application.Catalog.Exceptions;

public sealed class CatalogDuplicateException(string constraintName) : Exception
{
    public string ConstraintName { get; } = constraintName;
}
