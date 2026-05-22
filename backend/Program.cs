
using Backend.Hubs;
using Backend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace backend
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Configurar CORS para Angular (puerto 4200)
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", policy =>
                {
                    policy.WithOrigins("http://localhost:4200")
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials(); // Necesario para SignalR
                });
            });

            // 2. Agregar soporte para Controladores y SignalR
            builder.Services.AddControllers();
            builder.Services.AddSignalR();

            // 3. Registrar el Servicio Serie como Singleton
            builder.Services.AddSingleton<SerialService>();

            // 4. Agregar OpenAPI
            builder.Services.AddOpenApi();

            var app = builder.Build();

            // Configurar el pipeline HTTP
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            // Aplicar directivas de CORS antes del ruteo de controladores/hubs
            app.UseCors("CorsPolicy");

            app.UseAuthorization();

            // Mapear los Controladores REST
            app.MapControllers();

            // Mapear el Hub de SignalR para WebSockets
            app.MapHub<SerialHub>("/hubs/serial");

            // Forzar a escuchar en http://localhost:5200
            app.Run("http://localhost:5200");
        }
    }
}

