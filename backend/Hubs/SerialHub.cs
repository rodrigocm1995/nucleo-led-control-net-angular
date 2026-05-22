using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Backend.Hubs
{
    public class SerialHub : Hub
    {
        // El Hub puede permanecer vacío ya que toda la emisión de datos la controla el 
        // servicio singleton 'SerialService' utilizando IHubContext<SerialHub>.
        // Sin embargo, permite que los clientes se conecten mediante WebSockets directamente.
        
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }
    }
}
