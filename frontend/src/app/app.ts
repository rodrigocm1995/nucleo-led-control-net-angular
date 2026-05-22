import { Component, OnInit, AfterViewInit, inject, signal, effect, ElementRef, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SerialService } from './services/serial.service';

declare var Prism: any; // Declarar Prism para coloreado de código C

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit, AfterViewInit {
  // Inyección de servicios usando el patrón moderno de Angular
  public readonly serialService = inject(SerialService);

  // Elemento de consola para auto-scroll
  @ViewChild('serialTerminal') private terminalContainer!: ElementRef;

  // Estados locales para los selectores de la UI
  public readonly availablePorts = signal<string[]>([]);
  public readonly selectedPort = signal<string>('');
  public readonly selectedBaudRate = signal<number>(115200);

  // Estado del tema (Modo Oscuro / Modo Claro)
  public readonly isDarkMode = signal<boolean>(true);

  // Lista de velocidades estándar
  public readonly baudRates: number[] = [9600, 19200, 38400, 57600, 115200];

  constructor() {
    // Efecto reactivo para mantener sincronizados los dropdowns si hay conexión o cambios guardados en el servidor
    effect(() => {
      const isConnected = this.serialService.isConnected();
      const activePort = this.serialService.activePort();
      const activeBaud = this.serialService.activeBaudRate();
      const savedPort = this.serialService.savedPort();
      const savedBaud = this.serialService.savedBaudRate();

      if (isConnected && activePort) {
        this.selectedPort.set(activePort);
        this.selectedBaudRate.set(activeBaud);
      } else if (savedPort) {
        // Solo establecer si el usuario no ha seleccionado otro puerto manualmente
        if (!this.selectedPort()) {
          this.selectedPort.set(savedPort);
        }
        this.selectedBaudRate.set(savedBaud);
      }
    });

    // Efecto reactivo para auto-scroll cada vez que se añade una nueva línea en la terminal
    effect(() => {
      const lines = this.serialService.terminalLines();
      if (lines.length > 0) {
        // Esperar a que Angular dibuje el DOM antes de hacer scroll
        setTimeout(() => this.scrollToBottom(), 30);
      }
    });
  }

  ngOnInit() {
    this.loadPorts();
    this.initializeTheme();
  }

  // Inicializa el tema leyendo del localStorage
  private initializeTheme() {
    const savedTheme = localStorage.getItem('theme');
    if (savedTheme === 'light') {
      this.isDarkMode.set(false);
      document.body.classList.add('light-mode');
    } else {
      this.isDarkMode.set(true);
      document.body.classList.remove('light-mode');
    }
  }

  // Cambia el tema entre Día (Claro) y Noche (Oscuro)
  public onToggleTheme() {
    const currentMode = this.isDarkMode();
    if (currentMode) {
      this.isDarkMode.set(false);
      document.body.classList.add('light-mode');
      localStorage.setItem('theme', 'light');
    } else {
      this.isDarkMode.set(true);
      document.body.classList.remove('light-mode');
      localStorage.setItem('theme', 'dark');
    }
  }

  ngAfterViewInit() {
    this.highlightCode();
  }

  // Carga y refresca los puertos COM del servidor
  public loadPorts() {
    this.serialService.getAvailablePorts().subscribe({
      next: (ports) => {
        this.availablePorts.set(ports);
        
        // Si hay puertos y ninguno está seleccionado, seleccionar el primero
        if (ports.length > 0 && !this.selectedPort()) {
          // Si hay un puerto guardado y está disponible, seleccionarlo
          const saved = this.serialService.savedPort();
          if (saved && ports.includes(saved)) {
            this.selectedPort.set(saved);
          } else {
            this.selectedPort.set(ports[0]);
          }
        }
      },
      error: () => {
        this.serialService.logToTerminal('[Sistema] Error al leer los puertos del servidor.', 'error');
      }
    });
  }

  public onConnect() {
    const port = this.selectedPort();
    const baud = this.selectedBaudRate();
    
    if (!port) {
      this.serialService.logToTerminal('[Sistema] Selecciona un puerto COM válido.', 'error');
      return;
    }

    this.serialService.logToTerminal(`[Sistema] Enviando solicitud de conexión para ${port} a ${baud} bps...`, 'system');
    this.serialService.connect(port, baud).subscribe({
      next: (res) => {
        // El estado de conexión real se actualizará a través del Hub de SignalR (ReceiveStatus)
      },
      error: (err) => {
        const errorMsg = err.error?.Message || err.message || 'Error desconocido';
        this.serialService.logToTerminal(`[Sistema] Fallo al conectar: ${errorMsg}`, 'error');
      }
    });
  }

  public onDisconnect() {
    this.serialService.logToTerminal('[Sistema] Enviando solicitud de desconexión...', 'system');
    this.serialService.disconnect().subscribe({
      next: () => {
        // Desconectado con éxito
      },
      error: (err) => {
        this.serialService.logToTerminal(`[Sistema] Error al desconectar: ${err.message}`, 'error');
      }
    });
  }

  public onSync() {
    this.serialService.logToTerminal('[Sistema] Consultando estado del LED...', 'system');
    this.serialService.sendCommand('?').subscribe({
      error: (err) => {
        this.serialService.logToTerminal(`[Sistema] Error de sincronización: ${err.message}`, 'error');
      }
    });
  }

  public onLedToggle(event: Event) {
    const checkbox = event.target as HTMLInputElement;
    const cmd = checkbox.checked ? '1' : '0';
    
    // Cambiar estado local de manera optimista
    this.serialService.ledState.set(checkbox.checked);

    this.serialService.sendCommand(cmd).subscribe({
      error: (err) => {
        // Revertir si falla
        this.serialService.ledState.set(!checkbox.checked);
        this.serialService.logToTerminal(`[Sistema] Error al enviar comando del LED: ${err.message}`, 'error');
      }
    });
  }

  public onClearConsole() {
    this.serialService.clearTerminal();
  }

  private scrollToBottom() {
    if (this.terminalContainer) {
      const el = this.terminalContainer.nativeElement;
      el.scrollTop = el.scrollHeight;
    }
  }

  private highlightCode() {
    if (typeof Prism !== 'undefined') {
      Prism.highlightAll();
    }
  }
}
