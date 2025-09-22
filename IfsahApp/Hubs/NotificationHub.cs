using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace IfsahApp.Hubs
{
    [Authorize(Roles = "Admin,Examiner")]
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            // أي شخص معه دور Admin أو Examiner
            await Groups.AddToGroupAsync(Context.ConnectionId, "admins");

            await base.OnConnectedAsync();
        }
    }
}
