using Microsoft.AspNetCore.SignalR;

namespace CTSHIPDashboard.Hubs
{
    public class AnalyticsHub : Hub
    {
        public async Task SendUpdate(string message)
        {
            await Clients.All.SendAsync("ReceiveUpdate", message);
        }
    }
}
