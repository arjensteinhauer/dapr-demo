using Dapr;
using Dapr.Client;
using ilionx.MicroServices.Stateful.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ilionx.MicroServices.Stateful.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ShapeListController : ControllerBase
    {
        private const string shapeListStateName = "statestore";

        private readonly IHubContext<ShapeHub, IShapeEvents> _hubContext;
        private readonly ILogger<ShapeListController> _logger;
        private readonly StateOptions _stateOptions = new StateOptions
        {
            Concurrency = ConcurrencyMode.FirstWrite,
            Consistency = ConsistencyMode.Eventual,
            RetryOptions = new RetryOptions
            {
                RetryMode = RetryMode.Exponential,
                RetryInterval = TimeSpan.FromMilliseconds(100),
                RetryThreshold = 3
            }
        };

        public ShapeListController(IHubContext<ShapeHub, IShapeEvents> hubContext, ILogger<ShapeListController> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpGet("{clientId}")]
        public async Task<List<Guid>> GetAll(Guid clientId, [FromServices] DaprClient client)
        {
            var shapeList = await GetShapeListFromState(clientId.ToString("N"), client);
            return shapeList.Value;
        }

        [Topic("addShape")]
        [HttpPost]
        public async Task AddShape(ShapeActorId shapeActorId, [FromServices] DaprClient client)
        {
            var shapeList = await GetShapeListFromState(shapeActorId.ClientId.ToString("N"), client);
            if (!shapeList.Value.Any(shapeId => shapeId == shapeActorId.ShapeId))
            {
                shapeList.Value.Add(shapeActorId.ShapeId);
                await shapeList.SaveAsync(_stateOptions);
            }
        }

        [Topic("deleteShape")]
        [HttpDelete]
        public async Task DeleteShape(ShapeActorId shapeActorId, [FromServices] DaprClient client)
        {
            var shapeList = await GetShapeListFromState(shapeActorId.ClientId.ToString("N"), client);
            shapeList.Value.Remove(shapeActorId.ShapeId);
            await shapeList.SaveAsync(_stateOptions);
        }

        [Topic("onUpdatedShapeLocation")]
        [HttpPost("onUpdatedShapeLocation")]
        public async Task OnUpdatedShapeLocation(ShapeActorId shapeActorId)
        {
            await _hubContext.Clients.All.OnUpdatedShapeLocation(shapeActorId.ShapeId);
        }

        private async Task<StateEntry<List<Guid>>> GetShapeListFromState(string stateKey, DaprClient client)
        {
            var shapeList = await client.GetStateEntryAsync<List<Guid>>(shapeListStateName, stateKey);
            shapeList.Value = shapeList.Value?.Distinct().ToList() ?? new List<Guid>();
            return shapeList;
        }
    }

    public class ShapeActorId
    {
        public Guid ClientId { get; set; }

        public Guid ShapeId { get; set; }
    }
}
