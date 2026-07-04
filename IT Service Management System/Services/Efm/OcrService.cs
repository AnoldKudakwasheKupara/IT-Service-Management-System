namespace IT_Service_Management_System.Services.Efm
{
    /// <summary>
    /// Extracts searchable text from a document. Runs on the background queue after upload and
    /// populates <c>DocumentVersion.OcrText</c>, which the search index queries.
    /// </summary>
    public interface IOcrService
    {
        bool CanHandle(string? contentType);
        Task<string?> ExtractTextAsync(Stream content, string? contentType, CancellationToken ct = default);
    }

    /// <summary>
    /// Baseline implementation: reads embedded text from text/* files directly. Images and scanned
    /// PDFs return null here — plug in a real OCR engine (Tesseract, Azure AI Document Intelligence,
    /// AWS Textract) by implementing CanHandle/ExtractTextAsync for those content types.
    /// </summary>
    public class PlainTextOcrService : IOcrService
    {
        private const int MaxChars = 200_000;

        public bool CanHandle(string? contentType) =>
            contentType != null && (contentType.StartsWith("text/") || contentType == "application/csv");

        public async Task<string?> ExtractTextAsync(Stream content, string? contentType, CancellationToken ct = default)
        {
            if (!CanHandle(contentType)) return null;   // real OCR would handle image/PDF here
            using var reader = new StreamReader(content);
            var text = await reader.ReadToEndAsync(ct);
            return string.IsNullOrWhiteSpace(text) ? null
                 : (text.Length > MaxChars ? text[..MaxChars] : text);
        }
    }
}
