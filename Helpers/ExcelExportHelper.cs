// Helpers/ExcelExportHelper.cs
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

public static class ExcelExportHelper
{
    public static byte[] GenerateExcel<T>(IEnumerable<T> data, string sheetName = "Sheet1")
    {
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add(sheetName);

        // Header row
        ws.Cells[1, 1].LoadFromCollection(data, true);
        var headerRange = ws.Cells[1, 1, 1, ws.Dimension.End.Column];
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
        headerRange.Style.Fill.BackgroundColor.SetColor(Color.DarkGreen);
        headerRange.Style.Font.Color.SetColor(Color.White);
        headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        // Auto-fit & format numbers/dates
        ws.Cells[ws.Dimension.Address].AutoFitColumns();
        for (int col = 1; col <= ws.Dimension.End.Column; col++)
        {
            if (ws.Cells[2, col].Value is DateTime || ws.Cells[2, col].Value is DateTime?)
                ws.Column(col).Style.Numberformat.Format = "dd-MMM-yyyy";
        }

        return package.GetAsByteArray();
    }
}