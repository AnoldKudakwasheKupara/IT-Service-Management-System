namespace IT_Service_Management_System.Models
{
    /// <summary>The second-factor method a user has enrolled in.</summary>
    public enum MfaMethod
    {
        None = 0,
        Email = 1,          // one-time code emailed at sign-in
        Authenticator = 2   // TOTP code from an authenticator app (Google/Microsoft Authenticator, Authy…)
    }
}
