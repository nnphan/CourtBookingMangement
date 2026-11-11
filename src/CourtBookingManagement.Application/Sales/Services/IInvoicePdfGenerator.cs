using CourtBookingManagement.Application.Sales.Models.Responses;

namespace CourtBookingManagement.Application.Sales.Services;

public interface IInvoicePdfGenerator
{
    byte[] Generate(InvoiceSnapshotResponse invoice);
}
