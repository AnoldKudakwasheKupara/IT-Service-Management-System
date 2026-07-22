using System.Globalization;
using System.Text.RegularExpressions;

namespace IT_Service_Management_System.Helpers.Efm
{
    /// <summary>Metadata guessed from a document's OCR/text content, used to pre-fill the upload form.</summary>
    public record ExtractedMetadata(
        DateTime? IssueDate,
        DateTime? ExpiryDate,
        string? DocumentNumber,
        string? IdNumber);

    /// <summary>
    /// Best-effort extraction of expiry date, issue date, document number and national-ID number
    /// from OCR text. Heuristic and label-driven — findings are suggestions the user can override,
    /// never authoritative. Returns nulls rather than guessing when confidence is low.
    /// </summary>
    public static class DocumentMetadataExtractor
    {
        private static readonly string[] DateFormats =
        {
            "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "d.M.yyyy",
            "yyyy-MM-dd", "yyyy/MM/dd",
            "dd MMM yyyy", "d MMM yyyy", "dd MMMM yyyy", "d MMMM yyyy",
            "MMM dd, yyyy", "MMMM dd, yyyy", "MMM d yyyy", "dd MMM, yyyy"
        };

        // A date token in most common written forms (numeric or with a month name).
        private const string DatePattern =
            @"(\d{1,2}[/\-.]\d{1,2}[/\-.]\d{2,4}" +
            @"|\d{4}[/\-.]\d{1,2}[/\-.]\d{1,2}" +
            @"|\d{1,2}\s+[A-Za-z]{3,9},?\s+\d{4}" +
            @"|[A-Za-z]{3,9}\s+\d{1,2},?\s+\d{4})";

        // Zimbabwe national ID, e.g. "63-1234567 X 42" (also accepts run-together variants).
        private static readonly Regex ZimId =
            new(@"\b\d{2}[-\s]?\d{6,7}[-\s]?[A-Za-z][-\s]?\d{2}\b", RegexOptions.Compiled);

        public static ExtractedMetadata Extract(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new ExtractedMetadata(null, null, null, null);

            var expiry = FindLabelledDate(text, "expir", "valid until", "valid thru", "date of expiry", "expiry date", "valid to")
                         ?? FindLabelledDate(text, "exp");
            var issue = FindLabelledDate(text, "issue", "date of issue", "issued on", "issued", "valid from", "date of issue");

            // If exactly two dates exist and no labels matched, assume earliest=issue, latest=expiry.
            if (expiry == null && issue == null)
            {
                var all = AllDates(text).Distinct().OrderBy(d => d).ToList();
                if (all.Count >= 2) { issue = all.First(); expiry = all.Last(); }
            }

            var id = ZimId.Match(text) is { Success: true } m ? Norm(m.Value) : null;
            var docNo = FindDocumentNumber(text) ?? id;

            return new ExtractedMetadata(issue, expiry, docNo, id);
        }

        private static DateTime? FindLabelledDate(string text, params string[] labels)
        {
            foreach (var label in labels)
            {
                // Match the label then the first date within the following ~40 characters.
                var rx = new Regex(Regex.Escape(label) + @"[^0-9A-Za-z]{0,20}" + DatePattern,
                    RegexOptions.IgnoreCase);
                var match = rx.Match(text);
                if (match.Success && TryDate(match.Groups[1].Value, out var d)) return d;
            }
            return null;
        }

        private static IEnumerable<DateTime> AllDates(string text)
        {
            foreach (Match m in Regex.Matches(text, DatePattern))
                if (TryDate(m.Value, out var d)) yield return d;
        }

        private static string? FindDocumentNumber(string text)
        {
            // "Passport No: AB123456", "Document Number - 998877", "Licence No. 4455"
            var rx = new Regex(
                @"(?:passport|document|licen[cs]e|certificate|permit|registration|ref(?:erence)?|serial|card|folio)\s*(?:no\.?|number|#|id)?\s*[:\-#]?\s*([A-Z0-9][A-Z0-9\-/]{4,24})",
                RegexOptions.IgnoreCase);
            foreach (Match m in rx.Matches(text))
            {
                var v = m.Groups[1].Value.Trim().Trim('-', '/');
                // Skip pure dates masquerading as numbers.
                if (v.Length >= 5 && v.Any(char.IsDigit) && !TryDate(v, out _)) return v;
            }
            return null;
        }

        private static bool TryDate(string raw, out DateTime date)
        {
            raw = raw.Trim().Replace("  ", " ");
            if (DateTime.TryParseExact(raw, DateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out date)) return true;
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                // Reject clearly implausible years to avoid OCR noise.
                if (date.Year is >= 1950 and <= 2100) return true;
            }
            date = default;
            return false;
        }

        private static string Norm(string s) =>
            Regex.Replace(s, @"\s+", " ").Trim().ToUpperInvariant();
    }
}
