import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: '/login', pathMatch: 'full' },
  { path: 'login',    loadComponent: () => import('./components/login/login.component').then(m => m.LoginComponent) },
  { path: 'register', loadComponent: () => import('./components/register/register.component').then(m => m.RegisterComponent) },
  { path: 'admin',    canActivate: [authGuard, roleGuard], data: { roles: ['Admin'] },    loadComponent: () => import('./components/admin-dashboard/admin-dashboard.component').then(m => m.AdminDashboardComponent) },
  { path: 'hr',       canActivate: [authGuard, roleGuard], data: { roles: ['HR'] },       loadComponent: () => import('./components/hr-dashboard/hr-dashboard.component').then(m => m.HrDashboardComponent) },
  { path: 'manager',  canActivate: [authGuard, roleGuard], data: { roles: ['Manager'] },  loadComponent: () => import('./components/manager-dashboard/manager-dashboard.component').then(m => m.ManagerDashboardComponent) },
  { path: 'employee', canActivate: [authGuard, roleGuard], data: { roles: ['Employee', 'Mentor'] }, loadComponent: () => import('./components/employee-dashboard/employee-dashboard.component').then(m => m.EmployeeDashboardComponent) },
  { path: 'intern',   canActivate: [authGuard, roleGuard], data: { roles: ['Intern'] },   loadComponent: () => import('./components/intern-dashboard/intern-dashboard.component').then(m => m.InternDashboardComponent) },
  { path: '**', redirectTo: '/login' }
];
