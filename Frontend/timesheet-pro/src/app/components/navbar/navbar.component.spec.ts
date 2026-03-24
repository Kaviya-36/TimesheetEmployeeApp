import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';

import { NavbarComponent } from './navbar.component';
import { AuthService } from '../../services/auth.service';
import { NotificationService } from '../../services/notification.service';
import { BreadcrumbService } from '../../services/breadcrumb.service';
import { signal } from '@angular/core';

describe('NavbarComponent', () => {
  let component: NavbarComponent;
  let fixture: ComponentFixture<NavbarComponent>;
  let authSpy: jasmine.SpyObj<AuthService>;
  let notifSpy: jasmine.SpyObj<NotificationService>;

  beforeEach(async () => {
    authSpy = jasmine.createSpyObj('AuthService', ['logout'], {
      username: signal<string | null>('Alice'),
      currentRole: signal<string | null>('Admin'),
      token: signal<string | null>(null),
      isLoggedIn: () => true
    });

    notifSpy = jasmine.createSpyObj('NotificationService',
      ['connect', 'markAllRead', 'markRead', 'pushLocal'], {
        notifications: signal([]),
        unreadCount:   signal(0),
        connected:     signal(false)
      }
    );

    await TestBed.configureTestingModule({
      imports: [NavbarComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        { provide: AuthService,         useValue: authSpy  },
        { provide: NotificationService, useValue: notifSpy },
        BreadcrumbService
      ]
    }).compileComponents();

    fixture   = TestBed.createComponent(NavbarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => expect(component).toBeTruthy());

  // ── initial() ───────────────────────────────────────────────────────────────
  it('initial() returns first letter of username uppercased', () => {
    expect(component.initial()).toBe('A');
  });

  it('initial() returns "U" when username is null', () => {
    (authSpy as any).username = signal<string | null>(null);
    expect(component.initial()).toBe('U');
  });

  it('initial() returns uppercase even for lowercase username', () => {
    (authSpy as any).username = signal<string | null>('bob');
    expect(component.initial()).toBe('B');
  });

  // ── toggleNotif ────────────────────────────────────────────────────────────
  it('toggleNotif should show notification panel', () => {
    expect(component.showNotif).toBeFalse();
    component.toggleNotif();
    expect(component.showNotif).toBeTrue();
  });

  it('toggleNotif should close profile panel when opening notifications', () => {
    component.showProfile = true;
    component.toggleNotif();
    expect(component.showProfile).toBeFalse();
    expect(component.showNotif).toBeTrue();
  });

  it('toggleNotif twice should close panel', () => {
    component.toggleNotif();
    component.toggleNotif();
    expect(component.showNotif).toBeFalse();
  });

  // ── toggleProfile ──────────────────────────────────────────────────────────
  it('toggleProfile should show profile menu', () => {
    expect(component.showProfile).toBeFalse();
    component.toggleProfile();
    expect(component.showProfile).toBeTrue();
  });

  it('toggleProfile should close notification panel when opening profile', () => {
    component.showNotif = true;
    component.toggleProfile();
    expect(component.showNotif).toBeFalse();
    expect(component.showProfile).toBeTrue();
  });

  // ── getNotifIcon ───────────────────────────────────────────────────────────
  it('should return correct icon for Timesheet', () => {
    expect(component.getNotifIcon('Timesheet')).toBe('📋');
  });

  it('should return correct icon for Leave', () => {
    expect(component.getNotifIcon('Leave')).toBe('🌴');
  });

  it('should return correct icon for Attendance', () => {
    expect(component.getNotifIcon('Attendance')).toBe('⏰');
  });

  it('should return default icon for unknown type', () => {
    expect(component.getNotifIcon('Unknown')).toBe('🔔');
  });

  it('should return icon for Approval type', () => {
    expect(component.getNotifIcon('Approval')).toBe('✅');
  });

  // ── Outside click ──────────────────────────────────────────────────────────
  it('outside click on unrelated element should close both panels', () => {
    component.showNotif   = true;
    component.showProfile = true;
    const fakeEvent = { target: document.createElement('div') } as any;
    component.onOutsideClick(fakeEvent);
    expect(component.showNotif).toBeFalse();
    expect(component.showProfile).toBeFalse();
  });
});
