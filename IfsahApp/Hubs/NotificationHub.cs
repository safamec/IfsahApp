using Microsoft.AspNetCore.Authorization;


using Microsoft.AspNetCore.SignalR;

using System.Security.Claims;

using System.Threading.Tasks;


namespace IfsahApp.Hubs

{


    [Authorize]

    public class NotificationHub : Hub

    {public override async Task OnConnectedAsync()



        {


            // check role from claims


            var role = Context.User?.FindFirstValue(ClaimTypes.Role);





            if (string.IsNullOrEmpty(role) || role != "Admin")


            {


                // Not an admin → reject connection


                Context.Abort();


                return;


            }





            // Admin user → join groups as before


            var idClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);


            var email   = Context.User?.FindFirstValue(ClaimTypes.Email) 


                          ?? Context.User?.Identity?.Name;


            var sam     = Context.User?.FindFirstValue(ClaimTypes.WindowsAccountName);





            if (!string.IsNullOrWhiteSpace(idClaim))


                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{idClaim}");


            if (!string.IsNullOrWhiteSpace(email))


                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{email}");


            if (!string.IsNullOrWhiteSpace(sam))


                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{sam}");




            await base.OnConnectedAsync();


        }

    }

}