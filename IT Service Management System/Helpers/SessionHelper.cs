namespace IT_Service_Management_System.Helpers
{
    public static class SessionHelper
    {
        public static bool IsAdmin(HttpContext context)
        {
            return context.Session.GetString("UserRole") == "Admin";
        }

        public static bool IsLoggedIn(HttpContext context)
        {
            return context.Session.GetInt32("UserId") != null;
        }
    }
}