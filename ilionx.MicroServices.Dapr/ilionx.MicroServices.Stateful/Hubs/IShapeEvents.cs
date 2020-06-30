using System;
using System.Threading.Tasks;

namespace ilionx.MicroServices.Stateful.Hubs
{
    /// <summary>
    /// Describes all shape events which can be published via the SingalR hub.
    /// </summary>
    public interface IShapeEvents
    {
        /// <summary>
        /// Event fired when the shape location has been updated.
        /// </summary>
        /// <param name="shapeId">The shape ID</param>
        /// <returns>Async task.</returns>
        Task OnUpdatedShapeLocation(Guid shapeId);
    }
}