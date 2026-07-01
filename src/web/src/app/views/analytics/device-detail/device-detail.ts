import { Component, computed, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe, DecimalPipe } from '@angular/common';
import { DeviceStoreService } from '../../../core/state/device-store.service';

@Component({
  standalone: true,
  selector: 'app-device-analytics-detail',
  imports: [DatePipe, DecimalPipe, RouterLink],
  templateUrl: './device-detail.html',
  styleUrl: './device-detail.css'
})
export class DeviceAnalyticsDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly store = inject(DeviceStoreService);

  protected readonly device = computed(() => {
    const deviceId = this.route.snapshot.paramMap.get('id');
    if (!deviceId) {
      return null;
    }

    return this.store.devices().find((item) => item.deviceId === deviceId) ?? null;
  });
}
