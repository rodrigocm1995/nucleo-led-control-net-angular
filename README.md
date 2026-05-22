# Panel de Control LED para STM32 Nucleo (C# & Angular)

Este proyecto es una solución completa tipo cliente-servidor para controlar el LED físico (`LD2`) de una tarjeta de desarrollo **STM32 Nucleo-F303RE** desde una aplicación web moderna y reactiva. 

## Arquitectura de la Solución

- **Backend (C# / .NET 9 Web API + SignalR)**: Se conecta al hardware utilizando el puerto serie local (`System.IO.Ports`). Mantiene un bucle de lectura asíncrono y notifica a todos los clientes conectados sobre cambios de estado y tramas recibidas en tiempo real a través de WebSockets (SignalR).
- **Frontend (Angular 21.x Standalone)**: Ofrece una interfaz premium con estética *Glassmorphic*, tema oscuro, glows interactivos, consola de comandos auto-scrollable y visualización del código fuente con coloreado de sintaxis (PrismJS).

---

## Requisitos de Instalación

Para ejecutar este proyecto de forma local en tu computadora, debes tener instalado lo siguiente:

1. **.NET 9.0 SDK** (o superior)
   - Es necesario para compilar y ejecutar el backend en C#.
   - [Descargar .NET SDK](https://dotnet.microsoft.com/download)
2. **Node.js (LTS v18 o superior)**
   - Es necesario para administrar los paquetes del frontend de Angular y ejecutar su servidor de desarrollo.
   - [Descargar Node.js](https://nodejs.org/)
3. **Controlador ST-LINK USB** (STMicroelectronics)
   - Permite que Windows reconozca la tarjeta Nucleo conectada vía USB y cree el puerto COM virtual correspondiente.
   - Usualmente se instala de forma automática con STM32CubeIDE/Arduino IDE, o se descarga desde la página de STMicroelectronics.

---

## Cómo Ejecutar el Proyecto

### 1. Preparar la Tarjeta STM32
1. Carga el firmware que se encuentra en la sección **"Firmware de la Tarjeta"** de la interfaz web (o en los archivos del proyecto) utilizando STM32CubeIDE o Arduino IDE.
2. Conecta la placa Nucleo a un puerto USB de la computadora.

### 2. Iniciar el Servidor Backend (C#)
1. Abre tu terminal de comandos en la carpeta `backend`.
2. Ejecuta el comando para compilar e iniciar la API:
   ```bash
   dotnet run
   ```
3. El backend se iniciará y escuchará peticiones HTTP y WebSockets en la dirección: **`http://localhost:5200`**

### 3. Iniciar el Frontend (Angular)
1. Abre otra terminal de comandos en la carpeta `frontend`.
2. Instala las dependencias del proyecto (si es la primera vez que lo ejecutas):
   ```bash
   npm install
   ```
3. Inicia el servidor de desarrollo de Angular:
   ```bash
   npm start
   ```
4. El frontend compilará la aplicación y la servirá en la dirección: **`http://localhost:4200`**

### 4. Usar la Interfaz Web
Abre tu navegador de preferencia (Chrome, Edge, Firefox, Safari, etc.) e ingresa a:
👉 **[http://localhost:4200](http://localhost:4200)**

---

## Características Adicionales
- **Auto-conexión inteligente**: Al arrancar, el backend busca el último puerto COM y velocidad de baudios utilizados y se conecta automáticamente si están disponibles.
- **Persistencia local**: La última configuración de conexión exitosa se guarda localmente en el servidor (`serial-settings.json`).
- **Conectividad multiplataforma**: Gracias a que la comunicación serie es administrada en el backend, puedes acceder a la interfaz web y controlar la tarjeta desde otros dispositivos en la red local (celulares, tablets, laptops, etc.).
- **Sincronización multi-cliente**: El estado del LED y la terminal serie se actualizan instantáneamente en todos los navegadores abiertos a la vez gracias a SignalR.
