import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe, NgClass } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { DeviceApiService, DeviceStatusDto } from '../../core/devices/device-api.service';
import { DeviceStoreService } from '../../core/state/device-store.service';

@Component({
  selector: 'app-management-device',
  standalone: true,
  imports: [DatePipe, DecimalPipe, ReactiveFormsModule, RouterLink, NgClass],
  templateUrl: './device.html',
  styleUrl: './device.css',
})
export class Device {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private readonly deviceApiService = inject(DeviceApiService);
  private readonly store = inject(DeviceStoreService);

  protected readonly connectionState = this.store.connectionState;
  private readonly registryDevices = signal<ReadonlyArray<DeviceStatusDto>>([]);
  private readonly liveDevices = this.store.devices;
  protected readonly devices = computed(() => {
    const liveById = new Map(this.liveDevices().map((item) => [item.deviceId, item]));

    return this.registryDevices().map((device) => {
      const live = liveById.get(device.deviceId);

      if (!live) {
        return {
          ...device,
          currentTemperature: null as number | null,
          updatedAtUtc: device.lastHeartbeatUtc
        };
      }

      return {
        ...device,
        status: live.status,
        currentTemperature: live.currentTemperature,
        updatedAtUtc: live.lastUpdatedUtc,
        maxTemperatureThreshold: live.maxTemperatureThreshold
      };
    });
  });
  protected readonly loading = signal(false);
  protected readonly modalOpen = signal(false);
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');

  protected readonly form = this.formBuilder.group({
    deviceCode: ['', [Validators.required, Validators.minLength(2)]],
    maxTemperatureThreshold: [90, [Validators.required, Validators.min(1), Validators.max(1000)]]
  });

  constructor() {
    this.store.initialize();
    this.loadDevices();
  }

  protected openRegisterModal(): void {
    this.modalOpen.set(true);
  }

  protected closeRegisterModal(): void {
    this.modalOpen.set(false);
    this.form.reset({ deviceCode: '', maxTemperatureThreshold: 90 });
    this.errorMessage.set('');
  }

  protected submitRegister(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage.set('');
    this.submitting.set(true);

    this.deviceApiService
      .registerDevice(this.form.getRawValue())
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.submitting.set(false))
      )
      .subscribe({
        next: () => {
          this.closeRegisterModal();
          this.loadDevices();
        },
        error: () => {
          this.errorMessage.set('Device registration failed. Please check input or retry later.');
        }
      });
  }

  protected statusFlagClass(status: string): string {
    const key = status.toLowerCase();

    if (key.includes('run') || key.includes('active')) {
      return 'flag-running';
    }

    if (key.includes('maintenance')) {
      return 'flag-maintenance';
    }

    if (key.includes('stop') || key.includes('offline')) {
      return 'flag-stopped';
    }

    return 'flag-idle';
  }

  private loadDevices(): void {
    this.loading.set(true);

    this.deviceApiService
      .getAllDevices()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (result) => {
          this.registryDevices.set(result);
        },
        error: () => {
          this.errorMessage.set('Failed to load devices.');
        }
      });
  }
}
