using Backend.Hubs;
using Microsoft.AspNetCore.SignalR;
using System;
using System.IO;
using System.IO.Ports;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Backend.Services
{
    public class SerialService
    {
        private readonly IHubContext<SerialHub> _hubContext;
        private readonly ILogger<SerialService> _logger;
        private SerialPort? _serialPort;
        private string? _currentPort;
        private int _currentBaudRate = 115200;
        private string? _savedPort;
        private int _savedBaudRate = 115200;
        private Thread? _readThread;
        private bool _isRunning;
        private readonly object _lock = new();
        private readonly string _settingsPath;

        public SerialService(IHubContext<SerialHub> hubContext, ILogger<SerialService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
            _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "serial-settings.json");
            LoadSettings();
            
            // Proactivamente intentar auto-conectar al inicio si el puerto guardado sigue disponible en el sistema
            TryAutoConnect();
        }

        public string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }

        public object GetStatus()
        {
            lock (_lock)
            {
                return new
                {
                    IsConnected = _serialPort?.IsOpen ?? false,
                    PortName = _serialPort?.IsOpen == true ? _currentPort : null,
                    BaudRate = _serialPort?.IsOpen == true ? _currentBaudRate : 0,
                    SavedPort = _savedPort,
                    SavedBaudRate = _savedBaudRate
                };
            }
        }

        public bool Connect(string portName, int baudRate, out string errorMessage)
        {
            lock (_lock)
            {
                errorMessage = string.Empty;
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    Disconnect();
                }

                try
                {
                    _serialPort = new SerialPort(portName, baudRate)
                    {
                        ReadTimeout = 1000,
                        WriteTimeout = 1000,
                        NewLine = "\n" // Utiliza el carácter de salto de línea estándar para sincronizar tramas
                    };

                    _serialPort.Open();

                    // Limpiar buffers para evitar ruido de conexión inicial
                    _serialPort.DiscardInBuffer();
                    _serialPort.DiscardOutBuffer();

                    _currentPort = portName;
                    _currentBaudRate = baudRate;
                    _savedPort = portName;
                    _savedBaudRate = baudRate;
                    
                    SaveSettings(portName, baudRate);

                    _isRunning = true;
                    _readThread = new Thread(ReadLoop)
                    {
                        IsBackground = true,
                        Name = "SerialReadThread"
                    };
                    _readThread.Start();

                    _logger.LogInformation($"Puerto serie {portName} abierto con éxito a {baudRate} bps.");
                    
                    // Notificar cambio de estado y log a través de SignalR
                    _hubContext.Clients.All.SendAsync("ReceiveStatus", new { IsConnected = true, PortName = portName, BaudRate = baudRate });
                    _hubContext.Clients.All.SendAsync("ReceiveTxLog", $"[Sistema] Puerto COM conectado con éxito a {baudRate} bps.");
                    
                    // Consultar automáticamente el estado del LED en la placa tras iniciar conexión
                    Task.Run(async () =>
                    {
                        await Task.Delay(500); // Dar tiempo a la estabilización de conexión serie
                        SendCommand("?");
                    });

                    return true;
                }
                catch (Exception ex)
                {
                    errorMessage = ex.Message;
                    _logger.LogError($"Error al abrir el puerto {portName}: {ex.Message}");
                    _hubContext.Clients.All.SendAsync("ReceiveTxLog", $"[Sistema] Error de conexión en {portName}: {ex.Message}");
                    return false;
                }
            }
        }

        public void Disconnect()
        {
            lock (_lock)
            {
                _isRunning = false;
                
                if (_serialPort != null)
                {
                    try
                    {
                        if (_serialPort.IsOpen)
                        {
                            _serialPort.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error al cerrar el puerto serie: {ex.Message}");
                    }
                    finally
                    {
                        _serialPort.Dispose();
                        _serialPort = null;
                    }
                }

                _currentPort = null;

                _logger.LogInformation("Puerto serie desconectado.");
                _hubContext.Clients.All.SendAsync("ReceiveStatus", new { IsConnected = false, PortName = (string?)null, BaudRate = 0 });
                _hubContext.Clients.All.SendAsync("ReceiveTxLog", "[Sistema] Puerto serie desconectado.");
            }
        }

        public bool SendCommand(string cmd)
        {
            lock (_lock)
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                {
                    _logger.LogWarning("Intento de envío de comando sin puerto serie activo.");
                    return false;
                }

                try
                {
                    _serialPort.Write(cmd);
                    _logger.LogInformation($"Enviado comando serie: '{cmd}'");
                    _hubContext.Clients.All.SendAsync("ReceiveTxLog", $"Enviado: '{cmd}'");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error al transmitir comando serie: {ex.Message}");
                    _hubContext.Clients.All.SendAsync("ReceiveTxLog", $"Error al enviar: {ex.Message}");
                    return false;
                }
            }
        }

        private void ReadLoop()
        {
            while (_isRunning)
            {
                try
                {
                    if (_serialPort != null && _serialPort.IsOpen)
                    {
                        string line = _serialPort.ReadLine();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            string trimmedLine = line.Trim();
                            _logger.LogInformation($"Recibido del microcontrolador: \"{trimmedLine}\"");
                            
                            // Emitir el dato crudo a la terminal
                            _hubContext.Clients.All.SendAsync("ReceiveData", trimmedLine);

                            // Detectar estado del LED de manera proactiva para sincronizar la interfaz
                            string lowerLine = trimmedLine.ToLower();
                            if (lowerLine.Contains("encendido"))
                            {
                                _hubContext.Clients.All.SendAsync("ReceiveLedState", true);
                            }
                            else if (lowerLine.Contains("apagado"))
                            {
                                _hubContext.Clients.All.SendAsync("ReceiveLedState", false);
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (TimeoutException)
                {
                    // Ignorar time-out de lectura por polling
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error en el bucle de lectura del puerto serie: {ex.Message}");
                    
                    // Comprobar si se desconectó el dispositivo físicamente (USB desconectado)
                    lock (_lock)
                    {
                        if (_serialPort == null || !_serialPort.IsOpen)
                        {
                            HandleUnexpectedDisconnection();
                            break;
                        }
                    }
                    Thread.Sleep(500);
                }
            }
        }

        private void HandleUnexpectedDisconnection()
        {
            _logger.LogWarning("Desconexión inesperada de hardware detectada.");
            Disconnect();
            _hubContext.Clients.All.SendAsync("ReceiveTxLog", "[Sistema]⚠️ Advertencia: Conexión perdida con la tarjeta STM32 (Puerto COM cerrado inesperadamente).");
        }

        private void TryAutoConnect()
        {
            if (string.IsNullOrEmpty(_savedPort)) return;

            string[] availablePorts = GetAvailablePorts();
            bool isPortAvailable = Array.Exists(availablePorts, p => p.Equals(_savedPort, StringComparison.OrdinalIgnoreCase));

            if (isPortAvailable)
            {
                _logger.LogInformation($"Restableciendo conexión guardada de forma automática en el puerto: {_savedPort}");
                string error;
                Connect(_savedPort, _savedBaudRate, out error);
            }
            else
            {
                _logger.LogInformation($"El puerto guardado ({_savedPort}) no está disponible en este momento. Esperando selección manual del usuario.");
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    string json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<SettingsData>(json);
                    if (settings != null)
                    {
                        _savedPort = settings.SavedPort;
                        _savedBaudRate = settings.SavedBaudRate;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"No se pudo cargar la configuración de puerto: {ex.Message}");
            }
        }

        private void SaveSettings(string portName, int baudRate)
        {
            try
            {
                var settings = new SettingsData { SavedPort = portName, SavedBaudRate = baudRate };
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError($"No se pudo guardar la configuración de puerto: {ex.Message}");
            }
        }

        private class SettingsData
        {
            public string? SavedPort { get; set; }
            public int SavedBaudRate { get; set; }
        }
    }
}
