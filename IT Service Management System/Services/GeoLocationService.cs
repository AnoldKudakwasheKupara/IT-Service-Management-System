using System.Text.Json;

namespace IT_Service_Management_System.Services
{
    /// <summary>Resolves an IP address to a human-readable location via ip-api.com (best effort).</summary>
    public class GeoLocationService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<GeoLocationService> _logger;

        public GeoLocationService(IHttpClientFactory httpFactory, ILogger<GeoLocationService> logger)
        {
            _httpFactory = httpFactory;
            _logger = logger;
        }

        /// <summary>Returns a "City, Country" string, or "Localhost" / "Unknown".</summary>
        public async Task<string> ResolveAsync(string? ip)
        {
            if (string.IsNullOrEmpty(ip) || ip == "127.0.0.1" || ip == "::1")
                return "Localhost";

            try
            {
                var client = _httpFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(3);
                var json = await client.GetStringAsync($"http://ip-api.com/json/{ip}");
                var root = JsonDocument.Parse(json).RootElement;
                var city = root.TryGetProperty("city", out var c) ? c.GetString() : null;
                var country = root.TryGetProperty("country", out var co) ? co.GetString() : null;
                if (string.IsNullOrEmpty(country)) return "Unknown";
                return string.IsNullOrEmpty(city) ? country : $"{city}, {country}";
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Geo lookup failed for {Ip}", ip);
                return "Unknown";
            }
        }
    }
}
