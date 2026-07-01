import { Component, computed, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth/auth.service';
import { LiveDeviceTelemetry } from './core/realtime/simulation-data.model';
import { DeviceStoreService } from './core/state/device-store.service';

interface HeaderNotification {
  id: number;
  category: 'connection' | 'status' | 'alert';
  level: 'high' | 'medium' | 'low';
  title: string;
  body: string;
  time: string;
  read: boolean;
}

type NotificationFilter = 'all' | 'connection' | 'status' | 'alert' | 'unread';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  private readonly deviceStore = inject(DeviceStoreService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly deviceSnapshots = new Map<string, LiveDeviceTelemetry>();
  private readonly alertMuteUntilByKey = new Map<string, number>();
  private lastConnectionState: string | null = null;
  private nextNotificationId = 1;
  private readonly alertMuteWindowMs = 2 * 60 * 1000;

  protected readonly currentUser = this.authService.currentUser;
  protected readonly isAuthenticated = computed(() => !!this.currentUser());
  protected readonly notificationsOpen = signal(false);
  protected readonly notifications = signal<HeaderNotification[]>([]);
  protected readonly notificationFilter = signal<NotificationFilter>('all');

  protected readonly unreadCount = computed(() =>
    this.notifications().filter((item) => !item.read).length
  );

  protected readonly filteredNotifications = computed(() => {
    const filter = this.notificationFilter();
    const items = this.notifications();
    const byPriority = (list: HeaderNotification[]) =>
      [...list].sort((a, b) => {
        const score = this.priorityScore(b.level) - this.priorityScore(a.level);
        if (score !== 0) {
          return score;
        }

        return b.id - a.id;
      });

    if (filter === 'all') {
      return byPriority(items);
    }

    if (filter === 'unread') {
      return byPriority(items.filter((item) => !item.read));
    }

    return byPriority(items.filter((item) => item.category === filter));
  });

  protected readonly userInitial = computed(() => {
    const username = this.currentUser()?.username ?? '';
    return username.slice(0, 1).toUpperCase() || 'G';
  });

  private readonly authRealtimeEffect = effect(() => {
    if (this.isAuthenticated()) {
      this.deviceStore.initialize();
      return;
    }

    this.deviceStore.shutdown();
  });

  private readonly notificationStreamEffect = effect(() => {
    const connectionState = this.deviceStore.connectionState();
    const devices = this.deviceStore.devices();

    if (this.lastConnectionState !== connectionState) {
      if (connectionState === 'connected') {
        this.pushNotification(
          'connection',
          'medium',
          'Simulation Connected',
          'Realtime stream is now connected.'
        );
      }

      if (connectionState === 'disconnected' || connectionState === 'error') {
        this.pushNotification(
          'connection',
          'high',
          'Simulation Disconnected',
          'Realtime stream disconnected. Check backend or network path.'
        );
      }

      this.lastConnectionState = connectionState;
    }

    for (const device of devices) {
      const previous = this.deviceSnapshots.get(device.deviceId);

      if (!previous) {
        this.deviceSnapshots.set(device.deviceId, device);
        continue;
      }

      if (previous.sequence === device.sequence) {
        continue;
      }

      const wasAboveThreshold = previous.currentTemperature > previous.maxTemperatureThreshold;
      const isAboveThreshold = device.currentTemperature > device.maxTemperatureThreshold;

      if (!wasAboveThreshold && isAboveThreshold) {
        this.pushNotification(
          'alert',
          'high',
          `High Temperature: ${device.deviceCode}`,
          `Current ${device.currentTemperature.toFixed(1)} degC exceeded threshold ${device.maxTemperatureThreshold.toFixed(1)} degC.`,
          `alert:${device.deviceId}:high-temperature`
        );
      }

      if (previous.status !== device.status) {
        this.pushNotification(
          'status',
          'low',
          `Status Changed: ${device.deviceCode}`,
          `Status moved from ${previous.status} to ${device.status}.`
        );
      }

      this.deviceSnapshots.set(device.deviceId, device);
    }
  });

  protected toggleNotifications(): void {
    this.notificationsOpen.update((isOpen) => !isOpen);
  }

  protected markAllRead(): void {
    this.notifications.update((items) => items.map((item) => ({ ...item, read: true })));
  }

  protected setNotificationFilter(filter: NotificationFilter): void {
    this.notificationFilter.set(filter);
  }

  protected clearCurrentFilter(): void {
    const filter = this.notificationFilter();

    if (filter === 'all') {
      this.notifications.set([]);
      return;
    }

    if (filter === 'unread') {
      this.notifications.update((items) => items.filter((item) => item.read));
      return;
    }

    this.notifications.update((items) => items.filter((item) => item.category !== filter));
  }

  protected notificationTagClass(category: HeaderNotification['category']): string {
    switch (category) {
      case 'connection':
        return 'border-sky-300/40 bg-sky-500/20 text-sky-100';
      case 'status':
        return 'border-violet-300/40 bg-violet-500/20 text-violet-100';
      case 'alert':
        return 'border-rose-300/45 bg-rose-500/20 text-rose-100';
      default:
        return 'border-white/20 bg-slate-700/30 text-slate-100';
    }
  }

  protected notificationLevelClass(level: HeaderNotification['level']): string {
    switch (level) {
      case 'high':
        return 'border-rose-300/50 bg-rose-500/20 text-rose-100';
      case 'medium':
        return 'border-amber-300/50 bg-amber-500/20 text-amber-100';
      case 'low':
        return 'border-emerald-300/40 bg-emerald-500/20 text-emerald-100';
      default:
        return 'border-white/20 bg-slate-700/30 text-slate-100';
    }
  }

  protected logout(): void {
    this.authService.logout();
    this.notificationsOpen.set(false);
    void this.router.navigateByUrl('/login');
  }

  private pushNotification(
    category: HeaderNotification['category'],
    level: HeaderNotification['level'],
    title: string,
    body: string,
    dedupeKey?: string
  ): void {
    if (dedupeKey && this.shouldMuteNotification(dedupeKey)) {
      return;
    }

    const notification: HeaderNotification = {
      id: this.nextNotificationId++,
      category,
      level,
      title,
      body,
      time: new Date().toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
      read: false
    };

    this.notifications.update((items) => [notification, ...items].slice(0, 50));
  }

  private shouldMuteNotification(key: string): boolean {
    const now = Date.now();
    const muteUntil = this.alertMuteUntilByKey.get(key) ?? 0;

    if (now < muteUntil) {
      return true;
    }

    this.alertMuteUntilByKey.set(key, now + this.alertMuteWindowMs);
    return false;
  }

  private priorityScore(level: HeaderNotification['level']): number {
    switch (level) {
      case 'high':
        return 3;
      case 'medium':
        return 2;
      case 'low':
        return 1;
      default:
        return 0;
    }
  }
}
