using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace PresentationLayer.Hubs
{
    public class NewsHub : Hub
    {
        public async Task SendNewsUpdate(string message)
        {
            await Clients.All.SendAsync("ReceiveNewsUpdate", message);
        }
    }
}
