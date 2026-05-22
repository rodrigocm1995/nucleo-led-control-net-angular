import { Injectable, signal, WritableSignal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { Observable } from 'rxjs';

export interface SerialStatus {
  isConnected: boolean;
  portName: string | null;
  baudRate: number;
  savedPort: string | null;
  savedBaudRate: number;
}

export interface TerminalLine {
  text: string;
  type: 'system' | 'tx' | 'rx' | 'error';
  timestamp: string;
}

@Injectable({
  providedIn: 'root'
})
export class SerialService {
  private readonly apiUrl = 'http://localhost:5200/api/serial';
  private readonly hubUrl = 'http://localhost:5200/hubs/serial';
  private hubConnection: signalR.HubConnection | null = null;

  // Angular Signals para manejo de estado reactivo y eficiente
  public readonly isConnected = signal<boolean>(false);
  public readonly activePort = signal<string | null>(null);
  public readonly activeBaudRate = signal<number>(115200);
  public readonly savedPort = signal<string | null>(null);
  public readonly savedBaudRate = signal<number>(115200);
  
  public readonly ledState = signal<boolean>(false);
  public readonly terminalLines: WritableSignal<TerminalLine[]> = signal([]);

  constructor(private http: HttpClient) {
    this.checkInitialStatus();
    this.startSignalR();
  }

  // 1. HTTP REST Calls
  public getAvailablePorts(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/ports`);
  }

  public connect(portName: string, baudRate: number): Observable<any> {
    return this.http.post(`${this.apiUrl}/connect`, { portName, baudRate });
  }

  public disconnect(): Observable<any> {
    return this.http.post(`${this.apiUrl}/disconnect`, {});
  }

  public sendCommand(command: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/send`, { command });
  }

  // 2. Comprobar el estado inicial
  private checkInitialStatus() {
    this.http.get<SerialStatus>(`${this.apiUrl}/status`).subscribe({
      next: (status) => {
        this.isConnected.set(status.isConnected);
        this.activePort.set(status.portName);
        this.activeBaudRate.set(status.isConnected ? status.baudRate : status.savedBaudRate || 115200);
        this.savedPort.set(status.savedPort);
        this.savedBaudRate.set(status.savedBaudRate || 115200);
      },
      error: (err) => {
        this.logToTerminal('[Sistema] Error al conectar con el servidor backend. Asegúrate de que C# esté ejecutándose.', 'error');
      }
    });
  }

  // 3. Conexión SignalR (WebSockets)
  private startSignalR() {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl)
      .withAutomaticReconnect()
      .build();

    // Eventos del Hub
    this.hubConnection.on('ReceiveStatus', (status: { isConnected: boolean; portName: string | null; baudRate: number }) => {
      this.isConnected.set(status.isConnected);
      this.activePort.set(status.portName);
      if (status.isConnected) {
        this.activeBaudRate.set(status.baudRate);
      }
    });

    this.hubConnection.on('ReceiveData', (data: string) => {
      this.logToTerminal(`Recibido: "${data}"`, 'rx');
    });

    this.hubConnection.on('ReceiveTxLog', (log: string) => {
      // Determinar si es log del sistema o transmisión
      let type: 'system' | 'tx' | 'error' = 'system';
      if (log.startsWith('Enviado:')) {
        type = 'tx';
      } else if (log.includes('Error') || log.includes('⚠️')) {
        type = 'error';
      }
      this.logToTerminal(log, type);
    });

    this.hubConnection.on('ReceiveLedState', (state: boolean) => {
      this.ledState.set(state);
    });

    // Iniciar conexión
    this.hubConnection.start()
      .then(() => {
        this.logToTerminal('[Sistema] Conexión de tiempo real con el servidor establecida.', 'system');
      })
      .catch(err => {
        this.logToTerminal('[Sistema] No se pudo establecer conexión de tiempo real con el servidor.', 'error');
      });
  }

  // Helper para añadir líneas a la consola
  public logToTerminal(message: string, type: 'system' | 'tx' | 'rx' | 'error' = 'system') {
    const now = new Date();
    const timestamp = now.toTimeString().split(' ')[0] + '.' + String(now.getMilliseconds()).padStart(3, '0');
    
    const newLine: TerminalLine = {
      text: message,
      type,
      timestamp
    };

    this.terminalLines.update(lines => [...lines, newLine]);
  }

  public clearTerminal() {
    this.terminalLines.set([]);
    this.logToTerminal('[Sistema] Consola de comandos limpiada.', 'system');
  }
}
