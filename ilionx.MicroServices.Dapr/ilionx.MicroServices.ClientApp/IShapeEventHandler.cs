using System;
using System.Threading.Tasks;

namespace ilionx.MicroServices.ClientApp
{
    /// <summary>
    /// Describes the shape event handler.
    /// </summary>
    public interface IShapeEventHandler
    {
        /// <summary>
        /// Event handler for handling update shape location events.
        /// </summary>
        event EventHandler<Guid> OnUpdatedShapeLocation;

        /// <summary>
        /// Subscribe on update shape location events.
        /// </summary>
        /// <returns>Connection ID of the signalR connection.</returns>
        Task<string> SubscribeOnUpdatedShapeLocation();

        /// <summary>
        /// Disconnect the signalR connection.
        /// </summary>
        /// <returns>Async task.</returns>
        Task Disconnect();
    }
}
