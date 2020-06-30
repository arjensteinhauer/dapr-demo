using Microsoft.AspNetCore.SignalR;

namespace ilionx.MicroServices.Stateful.Hubs
{
    /// <summary>
    /// The SignalR shape hub.
    /// </summary>
    public class ShapeHub : Hub<IShapeEvents>
    {
    }
}
