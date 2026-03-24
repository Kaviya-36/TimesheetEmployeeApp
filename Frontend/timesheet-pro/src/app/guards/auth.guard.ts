import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { UserRole } from '../models';

export const authGuard: CanActivateFn = () => {
  const auth   = inject(AuthService);
  const router = inject(Router);
  if (auth.isLoggedIn()) return true;
  router.navigate(['/login']);
  return false;
};

export const roleGuard: CanActivateFn = (route) => {
  const auth    = inject(AuthService);
  const router  = inject(Router);
  const allowed = (route.data['roles'] ?? []) as UserRole[];

  if (!auth.isLoggedIn()) { router.navigate(['/login']); return false; }
  if (allowed.length === 0) return true;

  const role = auth.currentRole();
  if (role && allowed.includes(role)) return true;

  auth.redirectByRole();
  return false;
};
