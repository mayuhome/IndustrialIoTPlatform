import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { LiveDeviceTelemetry, RealtimeConnectionState } from './simulation-data.model';

export interface RealtimeSignalrHandlers {
  onPoint: (point: LiveDeviceTelemetry) => void;
  onStateChange: (state: RealtimeConnectionState) => void;
}

@Injectable({
  providedIn: 'root'
})
export class RealtimeSignalrService {
  private connection?: signalR.HubConnection;

  async connect(handlers: RealtimeSignalrHandlers): Promise<void> {
    if (this.connection && this.connection.state !== signalR.HubConnectionState.Disconnected) {
      return;
    }

    handlers.onStateChange('connecting');

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/simulation', {
        accessTokenFactory: () => localStorage.getItem('access_token') ?? localStorage.getItem('token') ?? ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();

    this.connection.onreconnecting(() => {
      handlers.onStateChange('reconnecting');
    });

    this.connection.onreconnected(() => {
      handlers.onStateChange('connected');
    });

    this.connection.onclose(() => {
      handlers.onStateChange('disconnected');
    });

    this.connection.on('ReceiveSimulationData', (payload: unknown) => {
      const point = this.normalizePayload(payload);
      if (!point) {
        return;
      }

      handlers.onPoint(point);
    });

    try {
      await this.connection.start();
      handlers.onStateChange('connected');
    } catch {
      handlers.onStateChange('error');
      throw new Error('SignalR connection failed.');
    }
  }

  async disconnect(): Promise<void> {
    if (!this.connection) {
      return;
    }

    await this.connection.stop();
    this.connection = undefined;
  }

  private normalizePayload(payload: unknown): LiveDeviceTelemetry | null {
    if (typeof payload !== 'object' || payload === null) {
      return null;
    }

    const record = payload as Record<string, unknown>;

    const deviceId = String(record['deviceId'] ?? record['DeviceId'] ?? '');
    if (!deviceId) {
      return null;
    }

    return {
      deviceId,
      deviceCode: String(record['deviceCode'] ?? record['DeviceCode'] ?? 'N/A'),
      status: String(record['status'] ?? record['Status'] ?? 'Unknown'),
      currentTemperature: Number(record['currentTemperature'] ?? record['CurrentTemperature'] ?? 0),
      maxTemperatureThreshold: Number(record['maxTemperatureThreshold'] ?? record['MaxTemperatureThreshold'] ?? 0),
      lastUpdatedUtc: String(record['lastUpdatedUtc'] ?? record['LastUpdatedUtc'] ?? new Date().toISOString()),
      sequence: Number(record['sequence'] ?? record['Sequence'] ?? 0)
    };
  }
}
