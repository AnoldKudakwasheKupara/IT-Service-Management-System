using IT_Service_Management_System.Models;

namespace IT_Service_Management_System.ViewModels
{
    public class AssetCreateViewModel
    {
        public Asset Asset { get; set; }

        public List<User> Users { get; set; }
    }
}