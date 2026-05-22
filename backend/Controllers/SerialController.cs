using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SerialController : ControllerBase
    {
        private readonly SerialService _serialService;

        public SerialController(SerialService serialService)
        {
            _serialService = serialService;
        }

        [HttpGet("ports")]
        public ActionResult<string[]> GetAvailablePorts()
        {
            return Ok(_serialService.GetAvailablePorts());
        }

        [HttpGet("status")]
        public ActionResult GetStatus()
        {
            return Ok(_serialService.GetStatus());
        }

        [HttpPost("connect")]
        public ActionResult Connect([FromBody] ConnectRequest request)
        {
            if (string.IsNullOrEmpty(request.PortName))
            {
                return BadRequest("El nombre del puerto es obligatorio.");
            }

            if (request.BaudRate <= 0)
            {
                return BadRequest("La velocidad de baudios debe ser mayor a cero.");
            }

            string error;
            bool success = _serialService.Connect(request.PortName, request.BaudRate, out error);

            if (success)
            {
                return Ok(new { Message = "Conectado exitosamente.", Success = true });
            }
            else
            {
                return StatusCode(500, new { Message = $"Error de conexión: {error}", Success = false });
            }
        }

        [HttpPost("disconnect")]
        public ActionResult Disconnect()
        {
            _serialService.Disconnect();
            return Ok(new { Message = "Desconectado exitosamente.", Success = true });
        }

        [HttpPost("send")]
        public ActionResult SendCommand([FromBody] SendCommandRequest request)
        {
            if (string.IsNullOrEmpty(request.Command))
            {
                return BadRequest("El comando no puede estar vacío.");
            }

            bool success = _serialService.SendCommand(request.Command);

            if (success)
            {
                return Ok(new { Message = "Comando enviado.", Success = true });
            }
            else
            {
                return BadRequest(new { Message = "No se pudo enviar el comando. ¿El puerto está conectado?", Success = false });
            }
        }
    }

    public class ConnectRequest
    {
        public string PortName { get; set; } = string.Empty;
        public int BaudRate { get; set; } = 115200;
    }

    public class SendCommandRequest
    {
        public string Command { get; set; } = string.Empty;
    }
}
