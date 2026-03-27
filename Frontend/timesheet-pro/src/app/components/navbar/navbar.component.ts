import { DatePipe } from '@angular/common';
import { Component, HostListener, inject, OnInit } from '@angular/core';
import { AuthService } from '../../services/auth.service';
import { NotificationService } from '../../services/notification.service';
import { BreadcrumbComponent } from '../breadcrumb/breadcrumb.component';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [DatePipe,  BreadcrumbComponent],
  templateUrl: './navbar.component.html',
  styleUrl:    './navbar.component.css'
})
export class NavbarComponent implements OnInit {
  readonly auth  = inject(AuthService);
  readonly notif = inject(NotificationService);

  showNotif   = false;
  showProfile = false;

  ngOnInit(): void {
    // Reconnect SignalR if user is already logged in (e.g. page refresh)
    if (this.auth.isLoggedIn() && !this.notif.connected()) {
      this.notif.connect();
    }
  }

  initial(): string {
    return (this.auth.username() ?? 'U')[0].toUpperCase();
  }

  toggleNotif() {
    this.showNotif   = !this.showNotif;
    this.showProfile = false;
  }

  toggleProfile() {
    this.showProfile = !this.showProfile;
    this.showNotif   = false;
  }

  @HostListener('document:click', ['$event'])
  onOutsideClick(e: MouseEvent) {
    const el = e.target as HTMLElement;
    if (!el.closest('.znotif-trigger') && !el.closest('.znotif-panel'))   this.showNotif   = false;
    if (!el.closest('.zprofile-trigger') && !el.closest('.zprofile-menu')) this.showProfile = false;
  }

  getNotifIcon(type: string): string {
    const map: Record<string, string> = {
      'Timesheet': '📋', 'Leave': '🌴', 'Attendance': '⏰',
      'Approval': '✅', 'Rejection': '❌', 'System': '🔔'
    };
    return map[type] ?? '🔔';
  }
}
