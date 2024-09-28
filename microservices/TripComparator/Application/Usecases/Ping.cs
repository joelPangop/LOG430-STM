using Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Usecases
{
    public class Ping {

        private readonly IRouteTimeProvider _routeTimeProvider;

        public Ping(IRouteTimeProvider routeTimeProvider)
        {
            _routeTimeProvider = routeTimeProvider;
        }

        public async Task<string> ping()
        {
            return await _routeTimeProvider.getIsAlive();
        }
    }
}
