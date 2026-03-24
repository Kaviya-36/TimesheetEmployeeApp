import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';

import { LoginComponent } from './login.component';
import { AuthService } from '../../services/auth.service';
import { NotificationService } from '../../services/notification.service';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let authSpy: jasmine.SpyObj<AuthService>;
  let notifSpy: jasmine.SpyObj<NotificationService>;

  beforeEach(async () => {
    authSpy  = jasmine.createSpyObj('AuthService',  ['login', 'redirectByRole', 'logout'], {
      token: () => null, currentRole: () => null, username: () => null,
      isLoggedIn: () => false
    });
    notifSpy = jasmine.createSpyObj('NotificationService', ['connect', 'pushLocal', 'markAllRead', 'markRead'], {
      notifications: () => [], unreadCount: () => 0, connected: () => false
    });

    await TestBed.configureTestingModule({
      imports: [LoginComponent, ReactiveFormsModule],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        { provide: AuthService,         useValue: authSpy  },
        { provide: NotificationService, useValue: notifSpy },
      ]
    }).compileComponents();

    fixture   = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  // ── Creation ───────────────────────────────────────────────────────────────
  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should start in "form" state', () => {
    expect(component.loginState()).toBe('form');
  });

  it('should start with loading = false', () => {
    expect(component.loading()).toBeFalse();
  });

  it('should start with no error', () => {
    expect(component.error()).toBe('');
  });

  // ── Form validation ────────────────────────────────────────────────────────
  it('should have an invalid form when empty', () => {
    expect(component.form.invalid).toBeTrue();
  });

  it('should be valid when username and password (6+ chars) are provided', () => {
    component.form.setValue({ username: 'admin', password: 'Admin@123' });
    expect(component.form.valid).toBeTrue();
  });

  it('should require username', () => {
    component.form.controls['username'].setValue('');
    expect(component.form.controls['username'].errors?.['required']).toBeTrue();
  });

  it('should require password at least 6 characters', () => {
    component.form.controls['password'].setValue('abc');
    expect(component.form.controls['password'].errors?.['minlength']).toBeTruthy();
  });

  it('should not call auth.login when form is invalid', () => {
    component.onSubmit();
    expect(authSpy.login).not.toHaveBeenCalled();
  });

  it('should mark all fields touched when submitted invalid', () => {
    component.onSubmit();
    expect(component.form.controls['username'].touched).toBeTrue();
    expect(component.form.controls['password'].touched).toBeTrue();
  });

  // ── fillDemo ────────────────────────────────────────────────────────────────
  it('fillDemo should populate form fields', () => {
    component.fillDemo({ username: 'admin', password: 'Admin@123' });
    expect(component.form.value.username).toBe('admin');
    expect(component.form.value.password).toBe('Admin@123');
  });

  it('should have 5 demo entries', () => {
    expect(component.demos.length).toBe(5);
  });

  it('all demos should have role, username, and password', () => {
    component.demos.forEach(d => {
      expect(d.role).toBeTruthy();
      expect(d.username).toBeTruthy();
      expect(d.password).toBeTruthy();
    });
  });

  // ── Password visibility ─────────────────────────────────────────────────────
  it('showPw should start false', () => {
    expect(component.showPw()).toBeFalse();
  });

  it('should toggle showPw', () => {
    component.showPw.update(v => !v);
    expect(component.showPw()).toBeTrue();
    component.showPw.update(v => !v);
    expect(component.showPw()).toBeFalse();
  });

  // ── Successful login ────────────────────────────────────────────────────────
  it('should call auth.login and redirect on success', fakeAsync(() => {
    authSpy.login.and.returnValue(of({
      userId: 1, username: 'admin', token: 'tok', role: 'Admin'
    } as any));
    component.form.setValue({ username: 'admin', password: 'Admin@123' });
    component.onSubmit();
    tick();
    expect(authSpy.login).toHaveBeenCalledWith({ username: 'admin', password: 'Admin@123' });
    expect(notifSpy.connect).toHaveBeenCalled();
    expect(authSpy.redirectByRole).toHaveBeenCalled();
  }));

  it('should set loading to false after successful login', fakeAsync(() => {
    authSpy.login.and.returnValue(of({ userId: 1, username: 'u', token: 't', role: 'Admin' } as any));
    component.form.setValue({ username: 'admin', password: 'Admin@123' });
    component.onSubmit();
    tick();
    expect(component.loading()).toBeFalse();
  }));

  // ── Error handling ──────────────────────────────────────────────────────────
  it('should set error message on 401', fakeAsync(() => {
    authSpy.login.and.returnValue(throwError(() => ({
      status: 401, error: { message: 'Invalid credentials' }
    })));
    component.form.setValue({ username: 'x', password: 'wrongpw' });
    component.onSubmit();
    tick();
    expect(component.error()).toContain('Invalid username or password');
    expect(component.loginState()).toBe('form');
  }));

  it('should switch to pending state when error message contains "pending"', fakeAsync(() => {
    authSpy.login.and.returnValue(throwError(() => ({
      status: 403, error: { message: 'Account is pending approval' }
    })));
    component.form.setValue({ username: 'newuser', password: 'Pass@123' });
    component.onSubmit();
    tick();
    expect(component.loginState()).toBe('pending');
  }));

  it('should switch to inactive state when error message contains "inactive"', fakeAsync(() => {
    authSpy.login.and.returnValue(throwError(() => ({
      status: 403, error: { message: 'Account is inactive' }
    })));
    component.form.setValue({ username: 'user1', password: 'Pass@123' });
    component.onSubmit();
    tick();
    expect(component.loginState()).toBe('inactive');
  }));

  it('should switch to inactive state when error contains "deactivated"', fakeAsync(() => {
    authSpy.login.and.returnValue(throwError(() => ({
      status: 403, error: { message: 'User has been deactivated' }
    })));
    component.form.setValue({ username: 'user1', password: 'Pass@123' });
    component.onSubmit();
    tick();
    expect(component.loginState()).toBe('inactive');
  }));

  it('should set loading false on error', fakeAsync(() => {
    authSpy.login.and.returnValue(throwError(() => ({ status: 401, error: {} })));
    component.form.setValue({ username: 'x', password: 'xxxxxx' });
    component.onSubmit();
    tick();
    expect(component.loading()).toBeFalse();
  }));

  // ── backToForm ───────────────────────────────────────────────────────────────
  it('backToForm should reset to form state', () => {
    component.loginState.set('pending');
    component.backToForm();
    expect(component.loginState()).toBe('form');
  });

  it('backToForm should clear error', () => {
    component.error.set('Some error');
    component.backToForm();
    expect(component.error()).toBe('');
  });

  it('backToForm should reset the form', () => {
    component.form.setValue({ username: 'admin', password: 'Admin@123' });
    component.backToForm();
    expect(component.form.value.username).toBeFalsy();
  });
});
