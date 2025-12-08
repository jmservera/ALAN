using ALAN.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace ALAN.Web.Hubs;

public class AgentHub : Hub
{
    public async Task SendStateUpdate(AgentState state)
    {
        await Clients.All.SendAsync("ReceiveStateUpdate", state);
    }
    
    public async Task SendThought(AgentThought thought)
    {
        await Clients.All.SendAsync("ReceiveThought", thought);
    }
    
    public async Task SendAction(AgentAction action)
    {
        await Clients.All.SendAsync("ReceiveAction", action);
    }
}
