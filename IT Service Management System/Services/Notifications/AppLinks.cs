namespace IT_Service_Management_System.Services.Notifications
{
    /// <summary>
    /// Builds absolute links for email sent from background jobs. A scheduled scan has no
    /// HttpContext, so it cannot discover the site's own address the way a controller does — the
    /// address comes from the "App:BaseUrl" setting instead. If that is unset the links degrade to
    /// relative paths (still correct inside the app, dead in an inbox) and we say so once in the log,
    /// which is a far better failure than emails that silently point at localhost.
    /// </summary>
    public class AppLinks
    {
        private readonly string _baseUrl;
        private bool _warned;
        private readonly ILogger<AppLinks> _logger;

        public AppLinks(IConfiguration config, ILogger<AppLinks> logger)
        {
            _logger = logger;
            _baseUrl = (config["App:BaseUrl"] ?? string.Empty).TrimEnd('/');
        }

        public bool IsConfigured => _baseUrl.Length > 0;

        /// <summary>Absolute URL for a controller action, e.g. To("Tickets", "Details", 42).</summary>
        public string To(string controller, string action, int? id = null)
        {
            var path = id.HasValue ? $"/{controller}/{action}/{id}" : $"/{controller}/{action}";
            if (IsConfigured) return _baseUrl + path;

            if (!_warned)
            {
                _warned = true;
                _logger.LogWarning(
                    "App:BaseUrl is not configured, so links in scheduled email will be relative and " +
                    "will not work from an inbox. Set it to the site's public address, e.g. " +
                    "\"App\": {{ \"BaseUrl\": \"https://itsm.example.com\" }}.");
            }

            return path;
        }

        /// <summary>The dashboard, used as the digest's call to action.</summary>
        public string Dashboard() => IsConfigured ? _baseUrl + "/" : "/";
    }
}
