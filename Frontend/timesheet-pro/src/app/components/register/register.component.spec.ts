import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';

import { RegisterComponent } from './register.component';
import { AuthService } from '../../services/auth.service';

describe('RegisterComponent', () => {
  let component: RegisterComponent;
  let fixture: ComponentFixture<RegisterComponent>;
  let authSpy: jasmine.SpyObj<AuthService>;

  beforeEach(async () => {
    authSpy = jasmine.createSpyObj('AuthService', ['register'], {
      token: () => null, currentRole: () => null, username: () => null, isLoggedIn: () => false
    });

    await TestBed.configureTestingModule({
      imports: [RegisterComponent, ReactiveFormsModule],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        { provide: AuthService, useValue: authSpy }
      ]
    }).compileComponents();

    fixture   = TestBed.createComponent(RegisterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  // ── Creation ───────────────────────────────────────────────────────────────
  it('should create', () => expect(component).toBeTruthy());
  it('should start with loading = false', () => expect(component.loading()).toBeFalse());
  it('should start with success = false', () => expect(component.success()).toBeFalse());
  it('should start with no error', () => expect(component.error()).toBe(''));

  // ── Static data ────────────────────────────────────────────────────────────
  it('should have 4 roles', () => expect(component.roles.length).toBe(4));
  it('roles should contain Employee, Intern, Manager, HR', () => {
    expect(component.roles).toContain('Employee');
    expect(component.roles).toContain('Intern');
    expect(component.roles).toContain('Manager');
    expect(component.roles).toContain('HR');
  });
  it('should have 4 departments', () => expect(component.departments.length).toBe(4));

  // ── Form validation ────────────────────────────────────────────────────────
  it('form should be invalid when empty', () => expect(component.form.invalid).toBeTrue());

  it('form should be valid with all required fields', () => {
    component.form.setValue({
      employeeId: 'EMP001', name: 'John Doe', email: 'john@test.com',
      password: 'Pass@123', phone: '', role: 'Employee', departmentId: '1'
    });
    expect(component.form.valid).toBeTrue();
  });

  it('should require employeeId', () => {
    component.form.controls['employeeId'].setValue('');
    expect(component.form.controls['employeeId'].errors?.['required']).toBeTrue();
  });

  it('should require name', () => {
    component.form.controls['name'].setValue('');
    expect(component.form.controls['name'].errors?.['required']).toBeTrue();
  });

  it('should require valid email', () => {
    component.form.controls['email'].setValue('not-an-email');
    expect(component.form.controls['email'].errors?.['email']).toBeTrue();
  });

  it('should require password min 6 chars', () => {
    component.form.controls['password'].setValue('abc');
    expect(component.form.controls['password'].errors?.['minlength']).toBeTruthy();
  });

  it('should require role', () => {
    component.form.controls['role'].setValue('');
    expect(component.form.controls['role'].errors?.['required']).toBeTrue();
  });

  it('should require departmentId', () => {
    component.form.controls['departmentId'].setValue('');
    expect(component.form.controls['departmentId'].errors?.['required']).toBeTrue();
  });

  // ── Submit invalid ─────────────────────────────────────────────────────────
  it('should not call register when form invalid', () => {
    component.onSubmit();
    expect(authSpy.register).not.toHaveBeenCalled();
  });

  it('should mark all touched when submitted invalid', () => {
    component.onSubmit();
    Object.values(component.form.controls).forEach(ctrl => {
      expect(ctrl.touched).toBeTrue();
    });
  });

  // ── Successful registration ────────────────────────────────────────────────
  it('should set success = true on successful registration', fakeAsync(() => {
    authSpy.register.and.returnValue(of({} as any));
    component.form.setValue({
      employeeId: 'EMP001', name: 'John', email: 'j@j.com',
      password: 'Pass@123', phone: '', role: 'Employee', departmentId: '1'
    });
    component.onSubmit();
    tick();
    expect(component.success()).toBeTrue();
    expect(component.loading()).toBeFalse();
  }));

  it('should pass correct payload to register', fakeAsync(() => {
    authSpy.register.and.returnValue(of({} as any));
    component.form.setValue({
      employeeId: 'EMP001', name: 'Alice', email: 'alice@test.com',
      password: 'Secret@1', phone: '9876543210', role: 'HR', departmentId: '2'
    });
    component.onSubmit();
    tick();
    const call = authSpy.register.calls.mostRecent().args[0];
    expect(call.employeeId).toBe('EMP001');
    expect(call.name).toBe('Alice');
    expect(call.email).toBe('alice@test.com');
    expect(call.role).toBe('HR');
    expect(call.departmentId).toBe(2);
  }));

  // ── Registration failure ───────────────────────────────────────────────────
  it('should set error message when registration fails', fakeAsync(() => {
    authSpy.register.and.returnValue(throwError(() => ({
      error: { message: 'Email already exists' }
    })));
    component.form.setValue({
      employeeId: 'E1', name: 'Bob', email: 'bob@test.com',
      password: 'Pass@123', phone: '', role: 'Employee', departmentId: '1'
    });
    component.onSubmit();
    tick();
    expect(component.error()).toContain('Email already exists');
    expect(component.success()).toBeFalse();
    expect(component.loading()).toBeFalse();
  }));

  it('should use fallback error message when none provided', fakeAsync(() => {
    authSpy.register.and.returnValue(throwError(() => ({})));
    component.form.setValue({
      employeeId: 'E1', name: 'Bob', email: 'b@b.com',
      password: 'Pass@123', phone: '', role: 'Employee', departmentId: '1'
    });
    component.onSubmit();
    tick();
    expect(component.error()).toContain('Registration failed');
  }));

  // ── Form accessor ──────────────────────────────────────────────────────────
  it('f getter should return form controls', () => {
    expect(component.f['name']).toBe(component.form.controls['name']);
    expect(component.f['email']).toBe(component.form.controls['email']);
  });
});
