import { Injectable, computed, signal } from '@angular/core';
import { LiveDeviceTelemetry, RealtimeConnectionState } from '../realtime/simulation-data.model';
import { RealtimeSignalrService } from '../realtime/realtime-signalr.service';

@Injectable({
  providedIn: 'root'
})
export class DeviceStoreService {
  private readonly entities = signal<Record<string, LiveDeviceTelemetry>>({});
  private readonly selectedDeviceId = signal<string | null>(null);
  readonly connectionState = signal<RealtimeConnectionState>('idle');
  private initialized = false;

  readonly devices = computed(() =>
    Object.values(this.entities()).sort((a, b) => a.deviceCode.localeCompare(b.deviceCode))
  );

  readonly selectedDevice = computed(() => {
    const selectedId = this.selectedDeviceId();
    const allDevices = this.devices();

    if (selectedId) {
      const match = allDevices.find((item) => item.deviceId === selectedId);
      if (match) {
        return match;
      }
    }

    return allDevices[0] ?? null;
  });

  constructor(private readonly realtimeSignalrService: RealtimeSignalrService) {}

  initialize(): void {
    if (this.initialized) {
      return;
    }

    this.initialized = true;

    void this.realtimeSignalrService
      .connect({
        onPoint: (point) => this.upsertPoint(point),
        onStateChange: (state) => this.connectionState.set(state)
      })
      .catch(() => {
        this.connectionState.set('error');
      });
  }

  selectDevice(deviceId: string): void {
    this.selectedDeviceId.set(deviceId);
  }

  private upsertPoint(point: LiveDeviceTelemetry): void {
    this.entities.update((current) => {
      const existing = current[point.deviceId];
      if (existing && existing.sequence > point.sequence) {
        return current;
      }

      return {
        ...current,
        [point.deviceId]: point
      };
    });

    if (!this.selectedDeviceId()) {
      this.selectedDeviceId.set(point.deviceId);
    }
  }
}
