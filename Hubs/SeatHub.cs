using Microsoft.AspNetCore.SignalR;

namespace CinePlex.Hubs
{
    public class SeatHub : Hub
    {
        public async Task JoinScreening(int screeningId) =>
            await Groups.AddToGroupAsync(Context.ConnectionId, $"screening-{screeningId}");

        public async Task JoinMarathon(int marathonId) =>
            await Groups.AddToGroupAsync(Context.ConnectionId, $"marathon-{marathonId}");
    }
}
