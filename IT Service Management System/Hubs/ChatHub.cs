using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace IT_Service_Management_System.Hubs
{
    public class ChatHub : Hub
    {
        public async Task SendMessage(int ticketId, string user, string message)
        {
            await Clients.Group(ticketId.ToString())
                .SendAsync("ReceiveMessage", user, message);
        }

        public async Task JoinTicketGroup(int ticketId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, ticketId.ToString());
        }
    }
}