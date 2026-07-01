import { Component, inject } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { DeviceStoreService } from '../../core/state/device-store.service';

@Component({
  selector: 'app-management-device',
  standalone: true,
  imports: [DatePipe, DecimalPipe, RouterLink],
  templateUrl: './device.html',
  styleUrl: './device.css',
})
export class Device {
  private readonly store = inject(DeviceStoreService);

  protected readonly devices = this.store.devices;
  protected readonly connectionState = this.store.connectionState;
}
