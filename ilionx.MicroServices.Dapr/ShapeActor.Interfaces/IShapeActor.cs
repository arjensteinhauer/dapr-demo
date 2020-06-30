using Dapr.Actors;
using ilionx.MicroServices.Models;
using System.Threading.Tasks;

namespace ilionx.MicroServices.Actors.Interface
{
    public interface IShapeActor : IActor
    {
        /// <summary>
        /// Gets the current shape position.
        /// </summary>
        /// <returns>The shape with the current position.</returns>
        Task<Shape> GetCurrentPositionAsync();

        Task UnregisterReminder();
    }
}
