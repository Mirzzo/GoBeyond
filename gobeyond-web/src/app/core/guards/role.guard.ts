import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

export const roleGuard: CanActivateFn = route => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const roles = (route.data?.['roles'] ?? []) as string[];
  if (!roles.length || authService.hasRole(roles)) {
    return true;
  }

  return router.parseUrl('/');
};
