using System.Reflection;
using PdfSharpCore.Fonts;

namespace CourtBookingManagement.Infrastructure.Sales;

internal sealed class NotoSansFontResolver : IFontResolver
{
    private const string RegularFontFace = "NotoSans-Regular";
    private const string RegularFontResource = "CourtBookingManagement.Infrastructure.Assets.Fonts.NotoSans-Regular.ttf";
    private readonly byte[] _regularFontData = ReadFontResource(RegularFontResource);

    public string DefaultFontName => "Noto Sans";

    public byte[] GetFont(string faceName) =>
        faceName switch
        {
            RegularFontFace => _regularFontData,
            _ => throw new InvalidOperationException($"Unknown invoice font face '{faceName}'.")
        };

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        if (!string.Equals(familyName, DefaultFontName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new FontResolverInfo(RegularFontFace, isBold, isItalic);
    }

    private static byte[] ReadFontResource(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded invoice font resource '{resourceName}' was not found.");
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
