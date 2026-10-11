using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace FreeCodeSpot.Attendance.IntegrationTests.Excel;

/// <summary>
/// Reads what is stored in a cell of an .xlsx file, straight from the XML inside it. A number
/// is stored as a number, a date as the number of days since 1899-12-30, and a time as a
/// part of a day. This is what Excel sees, so a test that reads it does not depend on how
/// a library decides to read a cell back.
/// </summary>
internal static class XlsxXml
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    /// <summary>The sheets are written in order, so sheet 1 is the first tab.</summary>
    public static XElement Cell(byte[] xlsx, int sheetNumber, string reference)
    {
        using var zip = new ZipArchive(new MemoryStream(xlsx));
        using var entry = zip.GetEntry($"xl/worksheets/sheet{sheetNumber}.xml")!.Open();

        return XDocument.Load(entry)
            .Descendants(Main + "c")
            .Single(cell => (string?)cell.Attribute("r") == reference);
    }

    public static double Number(byte[] xlsx, int sheetNumber, string reference) =>
        double.Parse((string)Cell(xlsx, sheetNumber, reference).Element(Main + "v")!, CultureInfo.InvariantCulture);

    public static string? Formula(byte[] xlsx, int sheetNumber, string reference) =>
        (string?)Cell(xlsx, sheetNumber, reference).Element(Main + "f");
}
