import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { NotificationService } from '../../services/notification.service';

type LoginState = 'form' | 'pending' | 'inactive';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
  private readonly fb    = inject(FormBuilder);
  private readonly auth  = inject(AuthService);
  private readonly notif = inject(NotificationService);

  readonly loading    = signal(false);
  readonly error      = signal('');
  readonly showPw     = signal(false);
  readonly loginState = signal<LoginState>('form');

  form = this.fb.group({
    username: ['', Validators.required],
    password: ['', [Validators.required, Validators.minLength(6)]]
  });

  onSubmit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.loginState.set('form');

    const { username, password } = this.form.value;

    this.auth.login({ username: username!, password: password! }).subscribe({

      // ✅ SUCCESS (only real success)
      next: (res: any) => {
        this.loading.set(false);

        this.notif.connect();
        this.auth.redirectByRole();
      },

      // 🔥 HANDLE ALL BUSINESS CASES HERE
      error: (err: any) => {
        this.loading.set(false);

        const message = (err?.error?.message ?? '').toLowerCase();

        // 🔥 INACTIVE USER (your current case)
        if (err.status === 400 && message.includes('inactive')) {
          this.loginState.set('inactive');
          return;
        }

        // ⏳ PENDING USER
        if (err.status === 400 && (
            message.includes('pending') ||
            message.includes('approval')
        )) {
          this.loginState.set('pending');
          return;
        }

        // 🔐 INVALID LOGIN
        if (err.status === 401) {
          this.error.set('Invalid username or password.');
          return;
        }

        // ❌ OTHER ERRORS
        this.error.set(err?.error?.message || 'Login failed.');
        console.error('Login error:', err);
      }
    });
  }

  backToForm() {
    this.loginState.set('form');
    this.error.set('');
    this.form.reset();
  }
}