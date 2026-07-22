using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IT_Service_Management_System.Helpers.Ims
{
    /// <summary>Excel (ClosedXML) and PDF (QuestPDF) exporters for the IMS registers &amp; report packs.</summary>
    public static class ImsExport
    {
        public const string XlsxContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private static readonly Color Brand = Color.FromHex("#2563EB");
        private static readonly Color Zebra = Color.FromHex("#F3F4F6");

        /// <summary>Builds a single-sheet workbook from headers + string rows.</summary>
        public static byte[] Xlsx(string sheetName, string[] headers, IEnumerable<string[]> rows)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(sheetName);
            for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
            var head = ws.Range(1, 1, 1, headers.Length);
            head.Style.Font.Bold = true;
            head.Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
            head.Style.Font.FontColor = XLColor.White;

            var r = 2;
            foreach (var row in rows)
            {
                for (int i = 0; i < row.Length; i++) ws.Cell(r, i + 1).Value = row[i];
                r++;
            }
            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        /// <summary>Builds a branded multi-section PDF (used for the executive summary / management-review pack).</summary>
        public static byte[] SummaryPdf(string title, string subtitle,
            IEnumerable<(string Section, string[] Headers, IEnumerable<string[]> Rows)> sections)
        {
            return Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(28);
                    page.DefaultTextStyle(t => t.FontSize(9));

                    page.Header().Column(col =>
                    {
                        col.Item().Text(title).FontSize(16).Bold().FontColor(Brand);
                        col.Item().Text(subtitle).FontSize(9).FontColor(Colors.Grey.Darken1);
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        foreach (var (section, headers, rows) in sections)
                        {
                            col.Item().PaddingTop(10).Text(section).FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
                            col.Item().PaddingTop(4).Table(table =>
                            {
                                table.ColumnsDefinition(c => { foreach (var _ in headers) c.RelativeColumn(); });
                                foreach (var h in headers)
                                    table.Cell().Background(Brand).Padding(4).Text(h).FontColor(Colors.White).Bold();
                                var zebra = false;
                                foreach (var row in rows)
                                {
                                    var bg = (zebra = !zebra) ? Zebra : Colors.White;
                                    foreach (var cell in row)
                                        table.Cell().Background(bg).Padding(4).Text(string.IsNullOrEmpty(cell) ? "—" : cell);
                                }
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Integrated Management System · Generated ").FontColor(Colors.Grey.Medium);
                        t.Span(DateTime.Now.ToString("dd MMM yyyy HH:mm")).FontColor(Colors.Grey.Medium);
                    });
                });
            }).GeneratePdf();
        }
    }
}
