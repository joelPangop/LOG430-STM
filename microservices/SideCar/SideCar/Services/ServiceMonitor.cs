using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ServiceMeshHelper;
using ServiceMeshHelper.BusinessObjects.InterServiceRequests;
using ServiceMeshHelper.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SideCar
{
    public class ServiceMonitor
    {
        private readonly HttpClient _httpClient;

        public ServiceMonitor()
        {
            _httpClient = new HttpClient();
        }

        // Dictionnaire avec les URL de santé des services
        private readonly Dictionary<string, string> _services = new()
    {
        //{ "STM", "http://stm:8080/health" },
        //{ "TripComparator", "http://tripcomparator:8080/health" },
        { "RouteTimeProvider", $"RouteTime/alive" }
    };

        // Méthode pour vérifier tous les services
        public async Task CheckAllServices()
        {
            foreach (var service in _services)
            {
                var isHealthy = await CheckService(service.Value);
                if (!isHealthy)
                {
                    RestartService(service.Key);
                }
            }
        }

        // Vérifie l'état d'un service via son URL
        private async Task<bool> CheckService(string url)
        {
            try
            {
                //var response = await _httpClient.GetAsync(url);
                //return response.IsSuccessStatusCode;

                var res = await RestController.Get(new GetRoutingRequest()
                {
                    TargetService = "RouteTimeProvider",
                    Endpoint = url,
                    Mode = LoadBalancingMode.RoundRobin
                });

                var restResponse = await res!.ReadAsync();

                return JsonConvert.DeserializeObject<string?>(restResponse.Content) == "IsAlive" ? true : false;
            }
            catch
            {
                return false;
            }
        }

        // Redémarre un service via Docker CLI
        private void RestartService(string serviceName)
        {
            Console.WriteLine($"Redémarrage du service {serviceName}...");

            // Commande pour redémarrer un conteneur Docker
            var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = $"-c \"docker restart {serviceName}\"";
            process.Start();
            process.WaitForExit();
        }
    }
}
