using ClosedXML.Excel;
using IT_Service_Management_System.ViewModels.Efm;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IT_Service_Management_System.Helpers.Efm
{
    /// <summary>
    /// Branded PDF (QuestPDF) and Excel (ClosedXML) exporters for the EFM audit trail and file
    /// completeness (compliance) reports. CSV exports remain in the controller; these add richer
    /// formats. Byte arrays are returned for the controller to stream as file downloads.
    /// </summary>
    public static class EfmExport
    {
        private static readonly Color Brand = Color.FromHex("#1D4ED8");
        private static readonly Color BrandDark = Color.FromHex("#1E3A8A");
        private static readonly Color Zebra = Color.FromHex("#F3F4F6");
        public const string XlsxContentType =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        // ── Audit trail ────────────────────────────────────────────────────────────
        public static byte[] AuditXlsx(IReadOnlyList<DocumentAuditRow> rows)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Document Audit");
            string[] headers = { "Timestamp", "Action", "Performed By", "Employee", "Document", "IP Address", "User Agent", "Details" };
            for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
            StyleHeader(ws.Range(1, 1, 1, headers.Length));

            var r = 2;
            foreach (var row in rows)
            {
                ws.Cell(r, 1).Value = row.Timestamp;
                ws.Cell(r, 1).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                ws.Cell(r, 2).Value = row.Action.ToString();
                ws.Cell(r, 3).Value = row.PerformedByName;
                ws.Cell(r, 4).Value = row.EmployeeName;
                ws.Cell(r, 5).Value = row.DocumentTitle;
                ws.Cell(r, 6).Value = row.IpAddress;
                ws.Cell(r, 7).Value = row.UserAgent;
                ws.Cell(r, 8).Value = row.Details;
                r++;
            }
            Finish(ws, headers.Length);
            return ToBytes(wb);
        }

        public static byte[] AuditPdf(IReadOnlyList<DocumentAuditRow> rows, string? scope)
        {
            return Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(24);
                    page.DefaultTextStyle(t => t.FontSize(8));

                    Header(page, "Document Audit Trail", scope);
                    page.Content().PaddingVertical(8).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(78); c.ConstantColumn(58); c.ConstantColumn(80);
                            c.ConstantColumn(90); c.RelativeColumn(2); c.ConstantColumn(66); c.RelativeColumn(3);
                        });
                        TableHead(table, "Time", "Action", "By", "Employee", "Document", "IP", "Details");
                        var zebra = false;
                        foreach (var row in rows)
                        {
                            var bg = (zebra = !zebra) ? Zebra : Colors.White;
                            Cell(table, bg, row.Timestamp.ToString("yyyy-MM-dd HH:mm"));
                            Cell(table, bg, row.Action.ToString());
                            Cell(table, bg, row.PerformedByName ?? "—");
                            Cell(table, bg, row.EmployeeName ?? "—");
                            Cell(table, bg, row.DocumentTitle ?? "—");
                            Cell(table, bg, row.IpAddress ?? "—");
                            Cell(table, bg, row.Details ?? "—");
                        }
                    });
                    Footer(page);
                });
            }).GeneratePdf();
        }

        // ── File completeness / compliance ──────────────────────────────────────────
        public static byte[] ComplianceXlsx(IReadOnlyList<ComplianceRow> rows)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("File Completeness");
            string[] headers = { "Employee", "Department", "Present", "Required", "Percent", "Missing Documents" };
            for (int i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
            StyleHeader(ws.Range(1, 1, 1, headers.Length));

            var r = 2;
            foreach (var row in rows)
            {
                ws.Cell(r, 1).Value = row.EmployeeName;
                ws.Cell(r, 2).Value = row.Department;
                ws.Cell(r, 3).Value = row.PresentCount;
                ws.Cell(r, 4).Value = row.RequiredCount;
                ws.Cell(r, 5).Value = row.Percent / 100.0;
                ws.Cell(r, 5).Style.NumberFormat.Format = "0%";
                ws.Cell(r, 6).Value = row.Missing;
                if (row.Percent < 100)
                    ws.Cell(r, 5).Style.Font.FontColor = row.Percent >= 75 ? XLColor.DarkOrange : XLColor.DarkRed;
                r++;
            }
            Finish(ws, headers.Length);
            return ToBytes(wb);
        }

        public static byte[] CompliancePdf(IReadOnlyList<ComplianceRow> rows)
        {
            var complete = rows.Count(x => x.Percent == 100);
            var avg = rows.Count > 0 ? (int)Math.Round(rows.Average(x => x.Percent)) : 0;
            return Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(28);
                    page.DefaultTextStyle(t => t.FontSize(9));

                    Header(page, "Employee File Completeness", $"{complete}/{rows.Count} complete · {avg}% average");
                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3); c.RelativeColumn(2); c.ConstantColumn(50);
                            c.ConstantColumn(55); c.RelativeColumn(4);
                        });
                        TableHead(table, "Employee", "Department", "Score", "%", "Missing");
                        var zebra = false;
                        foreach (var row in rows)
                        {
                            var bg = (zebra = !zebra) ? Zebra : Colors.White;
                            Cell(table, bg, row.EmployeeName);
                            Cell(table, bg, row.Department ?? "—");
                            Cell(table, bg, $"{row.PresentCount}/{row.RequiredCount}");
                            table.Cell().Background(bg).PaddingVertical(3).PaddingHorizontal(4)
                                .Text($"{row.Percent}%")
                                .FontColor(row.Percent == 100 ? Colors.Green.Darken2
                                    : row.Percent >= 75 ? Colors.Orange.Darken2 : Colors.Red.Darken2)
                                .SemiBold();
                            Cell(table, bg, string.IsNullOrWhiteSpace(row.Missing) ? "—" : row.Missing);
                        }
                    });
                    Footer(page);
                });
            }).GeneratePdf();
        }

        // ── shared PDF chrome ───────────────────────────────────────────────────────
        private static void Header(PageDescriptor page, string title, string? scope)
        {
            page.Header().Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Axis IT").FontSize(16).Bold().FontColor(Brand);
                        c.Item().Text("Employee File Management").FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                    row.ConstantItem(200).AlignRight().Column(c =>
                    {
                        c.Item().Text(title).FontSize(13).SemiBold().FontColor(BrandDark);
                        if (!string.IsNullOrWhiteSpace(scope))
                            c.Item().Text(scope).FontSize(8).FontColor(Colors.Grey.Medium);
                        c.Item().Text($"Generated {DateTime.Now:MMM dd, yyyy HH:mm}").FontSize(7).FontColor(Colors.Grey.Medium);
                    });
                });
                col.Item().PaddingTop(6).LineHorizontal(1.5f).LineColor(Brand);
            });
        }

        private static void Footer(PageDescriptor page)
        {
            page.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7).FontColor(Colors.Grey.Medium));
                t.Span("Axis IT · Confidential — ");
                t.CurrentPageNumber(); t.Span(" / "); t.TotalPages();
            });
        }

        private static void TableHead(TableDescriptor table, params string[] headers)
        {
            table.Header(h =>
            {
                foreach (var head in headers)
                    h.Cell().Background(Brand).Padding(4).Text(head).FontColor(Colors.White).SemiBold().FontSize(8);
            });
        }

        private static void Cell(TableDescriptor table, Color bg, string text) =>
            table.Cell().Background(bg).PaddingVertical(3).PaddingHorizontal(4).Text(text);

        // ── shared Excel chrome ─────────────────────────────────────────────────────
        private static void StyleHeader(IXLRange range)
        {
            range.Style.Fill.BackgroundColor = XLColor.FromHtml("#1D4ED8");
            range.Style.Font.FontColor = XLColor.White;
            range.Style.Font.Bold = true;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        private static void Finish(IXLWorksheet ws, int cols)
        {
            ws.SheetView.FreezeRows(1);
            ws.Range(1, 1, 1, cols).SetAutoFilter();
            ws.Columns(1, cols).AdjustToContents(1, 200.0, 60.0);
        }

        private static byte[] ToBytes(XLWorkbook wb)
        {
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }
}
