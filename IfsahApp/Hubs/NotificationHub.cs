using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Threading.Tasks;

namespace IfsahApp.Hubs
{
    public class NotificationHub : Hub
    {
      public override async Task OnConnectedAsync()
{
    var id = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
             ?? Context.GetHttpContext()?.Request.Query["userId"].ToString();
    if (!string.IsNullOrWhiteSpace(id))
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{id}");
    await base.OnConnectedAsync();
}

    }
}
