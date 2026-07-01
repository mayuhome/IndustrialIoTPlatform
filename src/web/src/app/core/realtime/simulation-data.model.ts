export interface LiveDeviceTelemetry {
  deviceId: string;
  deviceCode: string;
  status: string;
  currentTemperature: number;
  maxTemperatureThreshold: number;
  lastUpdatedUtc: string;
  sequence: number;
}

export type RealtimeConnectionState =
  | 'idle'
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'disconnected'
  | 'error';
