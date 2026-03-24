import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { signal } from '@angular/core';

import { SidebarComponent } from './sidebar.component';
import { AuthService } from '../../services/auth.service';

describe('SidebarComponent', () => {
  let component: SidebarComponent;
  let fixture: ComponentFixture<SidebarComponent>;
  let authSpy: jasmine.SpyObj<AuthService>;

  const makeAuth = (role: string | null) =>
    jasmine.createSpyObj('AuthService', ['logout'], {
      currentRole: signal<any>(role),
      username:    signal<string | null>('TestUser'),
      isLoggedIn:  () => !!role,
      token:       signal<string | null>(null)
    });

  beforeEach(async () => {
    authSpy = makeAuth('Admin');

    await TestBed.configureTestingModule({
      imports: [SidebarComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        { provide: AuthService, useValue: authSpy }
      ]
    }).compileComponents();

    fixture   = TestBed.createComponent(SidebarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => expect(component).toBeTruthy());

  // ── collapsed signal ───────────────────────────────────────────────────────
  it('should start expanded (collapsed = false)', () => {
    expect(component.collapsed()).toBeFalse();
  });

  it('should collapse when toggled', () => {
    component.collapsed.update(v => !v);
    expect(component.collapsed()).toBeTrue();
  });

  it('should expand when toggled twice', () => {
    component.collapsed.update(v => !v);
    component.collapsed.update(v => !v);
    expect(component.collapsed()).toBeFalse();
  });

  // ── visibleNav by role ─────────────────────────────────────────────────────
  it('Admin should see only Dashboard nav item', () => {
    const nav = component.visibleNav();
    expect(nav.length).toBe(1);
    expect(nav[0].label).toBe('Dashboard');
    expect(nav[0].route).toBe('/admin');
  });

  it('HR role should see HR Portal', () => {
    (authSpy as any).currentRole = signal<any>('HR');
    const nav = component.visibleNav();
    expect(nav.length).toBe(1);
    expect(nav[0].route).toBe('/hr');
  });

  it('Manager role should see Team View', () => {
    (authSpy as any).currentRole = signal<any>('Manager');
    const nav = component.visibleNav();
    expect(nav[0].route).toBe('/manager');
  });

  it('Employee role should see My Space', () => {
    (authSpy as any).currentRole = signal<any>('Employee');
    const nav = component.visibleNav();
    expect(nav[0].route).toBe('/employee');
  });

  it('Mentor role should see My Space (same as Employee)', () => {
    (authSpy as any).currentRole = signal<any>('Mentor');
    const nav = component.visibleNav();
    expect(nav[0].route).toBe('/employee');
  });

  it('Intern role should see Intern Hub', () => {
    (authSpy as any).currentRole = signal<any>('Intern');
    const nav = component.visibleNav();
    expect(nav[0].route).toBe('/intern');
  });

  it('null role should see no nav items', () => {
    (authSpy as any).currentRole = signal<any>(null);
    const nav = component.visibleNav();
    expect(nav.length).toBe(0);
  });

  it('unknown role should see no nav items', () => {
    (authSpy as any).currentRole = signal<any>('Unknown');
    const nav = component.visibleNav();
    expect(nav.length).toBe(0);
  });
});
