using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

using IfsahApp.Core.Enums;
using IfsahApp.Core.Models;
using IfsahApp.Hubs;
using IfsahApp.Infrastructure.Data;

namespace IfsahApp.Utils.Helpers 
{
    public static class NotificationHelper
    {
        public static async Task NotifyAdminsAsync(
            ApplicationDbContext context,
            IHubContext<NotificationHub> hub,
            Disclosure disclosure,
            IUrlHelper urlHelper)
        {
            var recipients = await context.Users
                .Where(u => u.IsActive && u.Role == Role.Admin)
                .Select(u => new { u.Id, u.Email, u.ADUserName })
                .ToListAsync();

            var notifications = recipients.Select(r => new Notification
            {
                RecipientId = r.Id,
                EventType = "Disclosure",
                Message = $"New disclosure {disclosure.DisclosureNumber} created",
                EmailAddress = r.Email,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            context.Notifications.AddRange(notifications);
            await context.SaveChangesAsync();

            string? url = urlHelper?.Action("Details", "Dashboard", new { id = disclosure.Id });

            foreach (var notification in notifications)
            {
                var payload = new
                {
                    id = notification.Id,
                    eventType = notification.EventType,
                    message = notification.Message,
                    createdAt = notification.CreatedAt.ToString("u"),
                    url
                };

                var recipient = recipients.FirstOrDefault(r => r.Id == notification.RecipientId);
                if (recipient == null) continue;

                var tasks = new List<Task>
                {
                    hub.Clients.Group($"user-{recipient.Id}").SendAsync("Notify", payload)
                };

                if (!string.IsNullOrWhiteSpace(recipient.Email))
                    tasks.Add(hub.Clients.Group($"user-{recipient.Email}").SendAsync("Notify", payload));

                if (!string.IsNullOrWhiteSpace(recipient.ADUserName))
                    tasks.Add(hub.Clients.Group($"user-{recipient.ADUserName}").SendAsync("Notify", payload));

                await Task.WhenAll(tasks);
            }
        }
    }
}
