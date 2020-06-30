using Refit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ilionx.MicroServices.ClientApp
{
    public interface IShapeListService
    {
        [Get("/shapeList/{clientId}")]
        Task<List<Guid>> GetAll(Guid clientId);
    }
}
