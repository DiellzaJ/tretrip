using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TreTrip.Api.Hubs;

[Authorize]
public class NotificationHub : Hub
{
}