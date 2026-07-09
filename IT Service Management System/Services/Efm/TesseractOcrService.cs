using SkiaSharp;
using Tesseract;

namespace IT_Service_Management_System.Services.Efm
{
    /// <summary>
    /// Real OCR for images and scanned PDFs using the Tesseract engine. Enabled by configuration
    /// (<c>EFM:Ocr:Provider = tesseract</c>); otherwise the app keeps the plain-text baseline.
    /// Scanned PDFs are rasterised to page images (via PDFtoImage) and each page is OCR'd.
    ///
    /// Any failure — missing native libs, absent <c>tessdata</c>, unreadable file — is logged and
    /// returns null, so a bad OCR run never breaks upload (OcrText is simply left empty).
    /// </summary>
    public class TesseractOcrService : IOcrService
    {
        private const int MaxChars = 400_000;
        private const int MaxPdfPages = 15;      // bound OCR time on large scans
        private const int RenderDpi = 200;

        private readonly string _tessData;
        private readonly string _language;
        private readonly ILogger<TesseractOcrService> _logger;

        public TesseractOcrService(IConfiguration config, ILogger<TesseractOcrService> logger)
        {
            _logger = logger;
            _tessData = config["EFM:Ocr:TessDataPath"]
                ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
            _language = config["EFM:Ocr:Language"] ?? "eng";
        }

        public bool CanHandle(string? contentType) =>
            contentType != null && (
                contentType.StartsWith("image/") ||
                contentType == "application/pdf" ||
                contentType.StartsWith("text/") ||
                contentType == "application/csv");

        public async Task<string?> ExtractTextAsync(Stream content, string? contentType, CancellationToken ct = default)
        {
            if (!CanHandle(contentType)) return null;

            // Embedded text (text/*, csv) never needs OCR — read it straight through.
            if (contentType!.StartsWith("text/") || contentType == "application/csv")
            {
                using var reader = new StreamReader(content);
                var t = await reader.ReadToEndAsync(ct);
                return Trim(t);
            }

            // Buffer the (already size-capped) upload so we can hand bytes to Tesseract/PDFium.
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();
            if (bytes.Length == 0) return null;

            try
            {
                if (!Directory.Exists(_tessData))
                {
                    _logger.LogWarning("Tesseract tessdata folder not found at {Path}; skipping OCR.", _tessData);
                    return null;
                }

                return contentType == "application/pdf"
                    ? OcrPdf(bytes, ct)
                    : OcrImage(bytes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tesseract OCR failed ({ContentType}); leaving OcrText empty.", contentType);
                return null;
            }
        }

        private string? OcrImage(byte[] imageBytes)
        {
            using var engine = new TesseractEngine(_tessData, _language, EngineMode.Default);
            using var pix = Pix.LoadFromMemory(imageBytes);
            using var page = engine.Process(pix);
            return Trim(page.GetText());
        }

        private string? OcrPdf(byte[] pdfBytes, CancellationToken ct)
        {
            using var engine = new TesseractEngine(_tessData, _language, EngineMode.Default);
            var sb = new System.Text.StringBuilder();
            int pageNo = 0;

            // CA1416: PDFium rasterization is supported on the Windows/Linux servers this app targets.
#pragma warning disable CA1416
            foreach (var bitmap in PDFtoImage.Conversion.ToImages(pdfBytes, options: new(Dpi: RenderDpi)))
            {
#pragma warning restore CA1416
                using (bitmap)
                {
                    ct.ThrowIfCancellationRequested();
                    if (++pageNo > MaxPdfPages) break;

                    using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                    using var pix = Pix.LoadFromMemory(data.ToArray());
                    using var page = engine.Process(pix);
                    var text = page.GetText();
                    if (!string.IsNullOrWhiteSpace(text)) sb.Append(text).Append('\n');
                    if (sb.Length > MaxChars) break;
                }
            }
            return Trim(sb.ToString());
        }

        private static string? Trim(string? text) =>
            string.IsNullOrWhiteSpace(text) ? null
            : (text.Length > MaxChars ? text[..MaxChars] : text);
    }
}
