using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace API.Realtime;

[Authorize]
public sealed class SimulationHub : Hub<ISimulationStreamClient>
{
}
