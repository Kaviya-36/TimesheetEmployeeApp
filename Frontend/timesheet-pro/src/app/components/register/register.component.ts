import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.component.html',
  styleUrl:    './register.component.css'
})
export class RegisterComponent {
  private fb   = inject(FormBuilder);
  private auth = inject(AuthService);

  readonly loading = signal(false);
  readonly error   = signal('');
  readonly success = signal(false);

  readonly roles = ['Employee', 'Intern', 'Manager', 'HR','Mentor'];
  readonly departments = [
    { id: 1, name: 'IT' }, { id: 2, name: 'HR' },
    { id: 3, name: 'Finance' }, { id: 4, name: 'Marketing' }
  ];

  form = this.fb.group({
    employeeId:   ['', Validators.required],
    name:         ['', [Validators.required, Validators.minLength(3)]],
    email:        ['', [Validators.required, Validators.email]],
    password:     ['', [Validators.required, Validators.minLength(6)]],
    phone:        ['', [Validators.required, Validators.minLength(10), Validators.maxLength(10), Validators.pattern('^[0-9]{10}$')]],
    role:         ['', Validators.required],
    departmentId: ['', Validators.required]
  });

  get f() { return this.form.controls; }

  onSubmit() {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.loading.set(true); this.error.set('');
    const v = this.form.getRawValue();
    this.auth.register({
      employeeId: v.employeeId!, name: v.name!, email: v.email!,
      password: v.password!, phone: v.phone ?? '',
      role: v.role!, departmentId: Number(v.departmentId!)
    }).subscribe({
      next: () => { this.loading.set(false); this.success.set(true); },
      error: (err: any) => {
        this.loading.set(false);
        this.error.set(err?.error?.message ?? 'Registration failed. Please try again.');
      }
    });
  }
}
