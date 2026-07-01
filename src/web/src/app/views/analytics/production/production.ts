import { Component, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { DeviceStoreService } from '../../../core/state/device-store.service';

@Component({
  standalone: true,
  selector: 'app-production-analytics',
  imports: [DecimalPipe, RouterLink],
  templateUrl: './production.html',
  styleUrl: './production.css'
})
export class ProductionAnalyticsComponent {
  private readonly store = inject(DeviceStoreService);

  protected readonly devices = this.store.devices;

  protected averageTemperature(): number {
    const devices = this.devices();
    if (devices.length === 0) {
      return 0;
    }

    const sum = devices.reduce((acc, item) => acc + item.currentTemperature, 0);
    return sum / devices.length;
  }
}
