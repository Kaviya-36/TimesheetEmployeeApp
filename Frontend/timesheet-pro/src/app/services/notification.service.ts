import { Injectable, signal } from '@angular/core';
import { AuthService } from './auth.service';
import { Notification } from '../models';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private hub: any = null;
  private reconnectTimer: any = null;

  readonly notifications = signal<Notification[]>([]);
  readonly unreadCount   = signal<number>(0);
  readonly connected     = signal<boolean>(false);

  constructor(private auth: AuthService) {}

  connect(): void {
    import('@microsoft/signalr').then((signalR) => {
      // Already connected or connecting
      if (this.hub && (this.hub.state === signalR.HubConnectionState.Connected ||
                       this.hub.state === signalR.HubConnectionState.Connecting)) return;

      this.hub = new signalR.HubConnectionBuilder()
        .withUrl('http://localhost:5117/notificationHub', {
          accessTokenFactory: () => this.auth.token() ?? ''
        })
        .withAutomaticReconnect({
          // Retry indefinitely with increasing delays
          nextRetryDelayInMilliseconds: (ctx) => {
            if (ctx.elapsedMilliseconds < 60_000) return 2000;
            if (ctx.elapsedMilliseconds < 300_000) return 10_000;
            return 30_000;
          }
        })
        .configureLogging(signalR.LogLevel.Warning)
        .build();

      this.hub.on('ReceiveNotification', (type: string, message: string, time: string) => {
        this.push({ type, message, time, read: false });
      });

      this.hub.onreconnecting(() => this.connected.set(false));
      this.hub.onreconnected(() => this.connected.set(true));
      this.hub.onclose(() => {
        this.connected.set(false);
        // Schedule a manual reconnect attempt after close
        this.scheduleReconnect();
      });

      this.startHub();
    }).catch(() => {});
  }

  private startHub(): void {
    this.hub?.start()
      .then(() => this.connected.set(true))
      .catch(() => {
        this.connected.set(false);
        this.scheduleReconnect();
      });
  }

  private scheduleReconnect(): void {
    if (this.reconnectTimer) return;
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      if (this.auth.isLoggedIn()) this.startHub();
    }, 5000);
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
    if (this.reconnectTimer) { clearTimeout(this.reconnectTimer); this.reconnectTimer = null; }
    this.hub?.stop?.();
    this.connected.set(false);
  }
}
