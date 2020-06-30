using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;

namespace ilionx.MicroServices.ClientApp
{
    /// <summary>
    /// The shape event handler for handling events received via SignalR.
    /// </summary>
    public class ShapeEventHandler : IShapeEventHandler
    {
        /// <summary>
        /// SignalR hub connection.
        /// </summary>
        private readonly HubConnection _connection;

        /// <summary>
        /// Event handler for handling update shape location events.
        /// </summary>
        public event EventHandler<Guid> OnUpdatedShapeLocation;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="connection"></param>
        public ShapeEventHandler(HubConnection connection)
        {
            _connection = connection;
            _connection.Closed += (ex) =>
            {
                // on disconnect, remove the current event handlers
                _connection.Remove("OnUpdatedShapeLocation");
                return Task.FromResult(true);
            };
            _connection.Reconnected += async (connectionId) =>
            {
                // on reconnect, subscribe on the event
                await SubscribeOnUpdatedShapeLocation().ConfigureAwait(false);
            };
        }

        /// <summary>
        /// Subscribe on update shape location events.
        /// </summary>
        /// <returns>Connection ID of the signalR connection.</returns>
        public async Task<string> SubscribeOnUpdatedShapeLocation()
        {
            _connection.On<Guid>("OnUpdatedShapeLocation", (shapeId) =>
            {
                OnUpdatedShapeLocation?.Invoke(this, shapeId);
            });

            if (_connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync().ConfigureAwait(false);
            }

            return _connection.ConnectionId;
        }

        /// <summary>
        /// Disconnect the signalR connection.
        /// </summary>
        /// <returns>Async task.</returns>
        public async Task Disconnect()
        {
            if (_connection.State == HubConnectionState.Connected)
            {
                await _connection.StopAsync().ConfigureAwait(false);
            }
        }
    }
}
