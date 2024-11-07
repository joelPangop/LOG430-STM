using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc;
using SideCar.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace SideCar
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // Méthode pour ajouter les services
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    builder =>
                    {
                        builder.AllowAnyOrigin()
                               .AllowAnyMethod()
                               .AllowAnyHeader();
                    });
            });

            // Ajoute les contrôleurs au conteneur de services
            services.AddControllers().PartManager.ApplicationParts.Add(new AssemblyPart(typeof(HealthCheckController).Assembly));

            // Ajoute un service singleton pour surveiller les services (par exemple)
            services.AddSingleton<ServiceMonitor>();
        }

        // Méthode pour configurer la requête HTTP pipeline
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseCors("AllowAll");

            // Utilise les contrôleurs (avec attributs)
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
