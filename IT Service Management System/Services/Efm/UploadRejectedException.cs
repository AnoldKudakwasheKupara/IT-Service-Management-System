namespace IT_Service_Management_System.Services.Efm
{
    /// <summary>
    /// Thrown when an upload is rejected before storage — e.g. malware detected or a duplicate of an
    /// existing file. Controllers catch this to report the reason per file instead of failing hard.
    /// </summary>
    public class UploadRejectedException : Exception
    {
        public UploadRejectedException(string message) : base(message) { }
    }
}
