using Dapr.Actors;
using Dapr.Actors.Runtime;
using Dapr.Client;
using ilionx.MicroServices.Actors.Interface;
using ilionx.MicroServices.Models;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace ilionx.MicroServices.Actors.Service
{
    public class ShapeActor : Actor, IShapeActor, IRemindable
    {
        private const string shapeStateName = "shape";
        private const string calculateNewPositionReminderName = "calculateNewPosition";
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        public ShapeActor(ActorService actorService, ActorId actorId)
            : base(actorService, actorId)
        {
        }

        /// <summary>
        /// Get the current shape position for this actor.
        /// </summary>
        /// <returns>The shape including the current position</returns>
        public async Task<Shape> GetCurrentPositionAsync()
        {
            // get from the actor state
            var result = await StateManager.TryGetStateAsync<Shape>(shapeStateName);
            return result.Value;
        }

        /// <summary>
        /// Handle the reminder.
        /// </summary>
        /// <param name="reminderName"></param>
        /// <param name="state"></param>
        /// <param name="dueTime"></param>
        /// <param name="period"></param>
        /// <returns></returns>
        public async Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            switch (reminderName)
            {
                // reminder for calculating a new shape position
                case calculateNewPositionReminderName:
                    await CalculateNewPosition(state);
                    break;
            }
        }

        /// <summary>
        /// Unregister all reminders for this actor.
        /// </summary>
        /// <returns></returns>
        public async Task UnregisterReminder()
        {
            await UnregisterReminderAsync(calculateNewPositionReminderName);
        }

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        /// <returns>Async task.</returns>
        protected override async Task OnActivateAsync()
        {
            Console.WriteLine($"Activate ShapeActor {this.Id.GetId()}");
            var actorIdParts = this.Id.GetId().Split('_');
            var clientId = actorIdParts[0];
            var shapeId = actorIdParts[1];

            // this is the first time the actor is activated --> initiate the actor state
            await StateManager.TryAddStateAsync(shapeStateName, CreateNewShape());

            // start a reminder for calculating new positions
            await RegisterReminderAsync(
                calculateNewPositionReminderName,
                null,
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(100));

            // add this shape instance to list (via publish)
            var client = new DaprClientBuilder().UseJsonSerializationOptions(_jsonOptions).Build();
            await client.PublishEventAsync<object>("addShape", new { ClientId = Guid.Parse(clientId), ShapeId = Guid.Parse(shapeId) });

            // base
            await base.OnActivateAsync();
        }

        protected override Task OnDeactivateAsync()
        {
            Console.WriteLine($"Deactivate ShapeActor {this.Id.GetId()}");

            return base.OnDeactivateAsync();
        }

        /// <summary>
        /// Create a new shape with a random position.
        /// </summary>
        /// <returns>The created shape.</returns>
        private Shape CreateNewShape()
        {
            var randomizer = new Random();
            var diff = new int[2] { -1, 1 };

            return new Shape()
            {
                X = randomizer.Next(10, 900),
                Y = randomizer.Next(10, 600),
                Angle = 0,
                DiffX = diff[randomizer.Next(0, 2)],
                DiffY = diff[randomizer.Next(0, 2)]
            };
        }

        /// <summary>
        /// Reminder callback for calculating the new position.
        /// </summary>
        /// <param name="state">Timer job state object.</param>
        /// <returns>Async task.</returns>
        private async Task CalculateNewPosition(object state)
        {
            // get the current state
            var result = await StateManager.TryGetStateAsync<Shape>(shapeStateName);
            if (result.HasValue)
            {
                // calculate the new position
                var shape = result.Value;

                if (shape.X > 900) shape.DiffX = -1;
                if (shape.X < 10) shape.DiffX = 1;
                if (shape.Y > 600) shape.DiffY = -1;
                if (shape.Y < 10) shape.DiffY = 1;

                shape.X += shape.DiffX;
                shape.Y += shape.DiffY;

                #region DEMO
                // demo: new implementation - let's also rotate the shape
                //shape.Angle++;
                #endregion DEMO

                // save the new position in the state
                await this.StateManager.SetStateAsync(shapeStateName, shape);

                // notify any subscribers the position has changed (publish on pub/sub)
                var actorIdParts = this.Id.GetId().Split('_');
                var clientId = actorIdParts[0];
                var shapeId = actorIdParts[1];
                var client = new DaprClientBuilder().UseJsonSerializationOptions(_jsonOptions).Build();
                await client.PublishEventAsync<object>("onUpdatedShapeLocation", new { ClientId = Guid.Parse(clientId), ShapeId = Guid.Parse(shapeId) });
            }
            else
            {
                Console.WriteLine($"ShapeActor {this.Id.GetId()} can't get the state.");
            }
        }
    }
}
