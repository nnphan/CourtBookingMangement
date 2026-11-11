using System.Globalization;
using CourtBookingManagement.Application.Sales.Models.Responses;
using CourtBookingManagement.Application.Sales.Services;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Pdf;

namespace CourtBookingManagement.Infrastructure.Sales;

public sealed class PdfInvoiceGenerator : IInvoicePdfGenerator
{
    private static readonly object FontResolverLock = new();
    private static volatile bool _fontResolverConfigured;
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly TimeSpan VietnamTimeOffset = TimeSpan.FromHours(7);

    public byte[] Generate(InvoiceSnapshotResponse invoice)
    {
        ConfigureFontResolver();
        using var document = new PdfDocument();
        document.Info.Title = $"Invoice {invoice.InvoiceNumber}";
        document.Info.Subject = "Badminton court POS invoice";

        var pageNumber = 0;
        var page = AddPage(document, invoice, ref pageNumber, out var graphics, out var y);
        try
        {
            foreach (var item in invoice.Items)
            {
                if (y > page.Height - 90)
                {
                    graphics.Dispose();
                    page = AddPage(document, invoice, ref pageNumber, out graphics, out y);
                }

                DrawItem(graphics, item, y);
                y += 24;
            }

            if (y > page.Height - 135)
            {
                graphics.Dispose();
                page = AddPage(document, invoice, ref pageNumber, out graphics, out y);
            }

            DrawTotals(graphics, invoice, y);
        }
        finally
        {
            graphics.Dispose();
        }

        using var output = new MemoryStream();
        document.Save(output, closeStream: false);
        return output.ToArray();
    }

    private static PdfPage AddPage(
        PdfDocument document,
        InvoiceSnapshotResponse invoice,
        ref int pageNumber,
        out XGraphics graphics,
        out double y)
    {
        pageNumber++;
        var page = document.AddPage();
        page.Size = PdfSharpCore.PageSize.A4;
        graphics = XGraphics.FromPdfPage(page);
        var titleFont = new XFont("Noto Sans", 18, XFontStyle.Bold);
        var headingFont = new XFont("Noto Sans", 11, XFontStyle.Bold);
        var bodyFont = new XFont("Noto Sans", 10, XFontStyle.Regular);
        var margin = 36d;

        graphics.DrawString(invoice.BranchName, titleFont, XBrushes.Black, margin, 42);
        if (!string.IsNullOrWhiteSpace(invoice.BranchAddress))
        {
            graphics.DrawString(invoice.BranchAddress, bodyFont, XBrushes.Black, margin, 60);
        }

        if (!string.IsNullOrWhiteSpace(invoice.BranchPhone))
        {
            graphics.DrawString($"Điện thoại: {invoice.BranchPhone}", bodyFont, XBrushes.Black, margin, 76);
        }

        graphics.DrawString("HÓA ĐƠN BÁN HÀNG", headingFont, XBrushes.Black, margin, 108);
        graphics.DrawString($"Số hóa đơn: {invoice.InvoiceNumber}", bodyFont, XBrushes.Black, margin, 128);
        graphics.DrawString(
            $"Ngày: {invoice.IssuedAt.ToOffset(VietnamTimeOffset).ToString("dd/MM/yyyy HH:mm", VietnameseCulture)}",
            bodyFont,
            XBrushes.Black,
            margin,
            145);
        graphics.DrawString($"Thu ngân: {invoice.CashierName}", bodyFont, XBrushes.Black, margin, 162);
        graphics.DrawString(
            $"Khách hàng: {invoice.CustomerName ?? "Khách lẻ"}",
            bodyFont,
            XBrushes.Black,
            margin,
            179);
        if (!string.IsNullOrWhiteSpace(invoice.CustomerPhone))
        {
            graphics.DrawString($"Điện thoại: {invoice.CustomerPhone}", bodyFont, XBrushes.Black, margin, 196);
            y = 230;
        }
        else
        {
            y = 213;
        }

        DrawTableHeader(graphics, y);
        y += 23;
        return page;
    }

    private static void DrawTableHeader(XGraphics graphics, double y)
    {
        var font = new XFont("Noto Sans", 9, XFontStyle.Bold);
        var right = new XStringFormat { Alignment = XStringAlignment.Far, LineAlignment = XLineAlignment.Center };
        graphics.DrawString("Sản phẩm", font, XBrushes.Black, 36, y);
        graphics.DrawString("SL", font, XBrushes.Black, new XRect(365, y - 10, 38, 18), right);
        graphics.DrawString("Đơn giá", font, XBrushes.Black, new XRect(405, y - 10, 75, 18), right);
        graphics.DrawString("Thành tiền", font, XBrushes.Black, new XRect(485, y - 10, 74, 18), right);
        graphics.DrawLine(XPens.DarkGray, 36, y + 7, 559, y + 7);
    }

    private static void DrawItem(XGraphics graphics, InvoiceSnapshotItemResponse item, double y)
    {
        var font = new XFont("Noto Sans", 9, XFontStyle.Regular);
        var right = new XStringFormat { Alignment = XStringAlignment.Far, LineAlignment = XLineAlignment.Center };
        var productName = item.ProductName.Length > 42
            ? string.Concat(item.ProductName.AsSpan(0, 39), "...")
            : item.ProductName;
        graphics.DrawString(productName, font, XBrushes.Black, 36, y);
        graphics.DrawString(item.Sku, font, XBrushes.Gray, 36, y + 11);
        graphics.DrawString(
            $"{item.Quantity.ToString("0.###", VietnameseCulture)} {item.Unit}",
            font,
            XBrushes.Black,
            new XRect(345, y - 5, 58, 18),
            right);
        graphics.DrawString(FormatMoney(item.UnitPrice), font, XBrushes.Black, new XRect(405, y - 5, 75, 18), right);
        graphics.DrawString(FormatMoney(item.LineTotal), font, XBrushes.Black, new XRect(485, y - 5, 74, 18), right);
    }

    private static void DrawTotals(XGraphics graphics, InvoiceSnapshotResponse invoice, double y)
    {
        var bodyFont = new XFont("Noto Sans", 10, XFontStyle.Regular);
        var totalFont = new XFont("Noto Sans", 12, XFontStyle.Bold);
        var right = new XStringFormat { Alignment = XStringAlignment.Far, LineAlignment = XLineAlignment.Center };
        y += 12;
        graphics.DrawLine(XPens.DarkGray, 350, y, 559, y);
        y += 22;
        graphics.DrawString("Tạm tính", bodyFont, XBrushes.Black, 350, y);
        graphics.DrawString(FormatMoney(invoice.SubtotalAmount), bodyFont, XBrushes.Black, new XRect(450, y - 12, 109, 20), right);
        if (invoice.DiscountAmount > 0)
        {
            y += 20;
            graphics.DrawString("Giảm giá", bodyFont, XBrushes.Black, 350, y);
            graphics.DrawString(FormatMoney(invoice.DiscountAmount), bodyFont, XBrushes.Black, new XRect(450, y - 12, 109, 20), right);
        }

        y += 24;
        graphics.DrawString("TỔNG CỘNG", totalFont, XBrushes.Black, 350, y);
        graphics.DrawString(FormatMoney(invoice.TotalAmount), totalFont, XBrushes.Black, new XRect(440, y - 15, 119, 24), right);
        y += 24;
        graphics.DrawString(
            $"Thanh toán: {invoice.PaymentMethodCode} - {invoice.PaymentStatus}",
            bodyFont,
            XBrushes.Black,
            350,
            y);
        y += 42;
        graphics.DrawString("Cảm ơn quý khách và hẹn gặp lại!", bodyFont, XBrushes.Black, 36, y);
    }

    private static string FormatMoney(decimal amount) =>
        $"{amount.ToString("#,##0.00", VietnameseCulture)} ₫";

    private static void ConfigureFontResolver()
    {
        if (_fontResolverConfigured)
        {
            return;
        }

        lock (FontResolverLock)
        {
            if (_fontResolverConfigured)
            {
                return;
            }

            GlobalFontSettings.FontResolver ??= new NotoSansFontResolver();
            _fontResolverConfigured = true;
        }
    }
}
