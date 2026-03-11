using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MyHotel.Web.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public async Task JoinHotelGroup(string hotelId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"hotel_{hotelId}");
    }

    public async Task LeaveHotelGroup(string hotelId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"hotel_{hotelId}");
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
        await base.OnConnectedAsync();
    }
}
