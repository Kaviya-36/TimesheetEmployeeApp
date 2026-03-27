import { Injectable, signal } from '@angular/core';
import { AuthService } from './auth.service';
import { Notification } from '../models';

/**
 * NotificationService
 * Uses dynamic import for SignalR so the app builds even without
 * @microsoft/signalr installed. Run: npm install @microsoft/signalr
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private hub: any = null;

  readonly notifications = signal<Notification[]>([]);
  readonly unreadCount   = signal<number>(0);
  readonly connected     = signal<boolean>(false);

  constructor(private auth: AuthService) {}

  connect(): void {
    import('@microsoft/signalr').then((signalR) => {
      if (this.hub?.state === 'Connected') return;

      this.hub = new signalR.HubConnectionBuilder()
        .withUrl('http://localhost:5117/notificationHub', {
          accessTokenFactory: () => this.auth.token() ?? ''
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // limited retries
        .configureLogging(signalR.LogLevel.None)
        .build();

      this.hub.on('ReceiveNotification', (type: string, message: string, time: string) => {
        this.push({ type, message, time, read: false });
      });

      this.hub.onreconnected(() => this.connected.set(true));
      this.hub.onclose(() => this.connected.set(false));

      this.hub.start()
        .then(() => this.connected.set(true))
        .catch(() => this.connected.set(false));
    }).catch(() => {});
  }

  pushLocal(type: string, message: string): void {
    this.push({ type, message, time: new Date().toISOString(), read: false });
  }

  private push(n: Notification): void {
    this.notifications.update(ns => [n, ...ns].slice(0, 50));
    this.unreadCount.update(c => c + 1);
  }

  markAllRead(): void {
    this.notifications.update(ns => ns.map(n => ({ ...n, read: true })));
    this.unreadCount.set(0);
  }

  markRead(index: number): void {
    this.notifications.update(ns => ns.map((n, i) => i === index ? { ...n, read: true } : n));
    if (this.unreadCount() > 0) this.unreadCount.update(c => c - 1);
  }

  disconnect(): void {
    this.hub?.stop?.();
    this.connected.set(false);
  }
}
