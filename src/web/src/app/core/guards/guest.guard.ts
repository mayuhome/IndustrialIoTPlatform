import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';

export const guestGuard: CanActivateFn = () => {
  const token = localStorage.getItem('access_token') ?? localStorage.getItem('token');

  if (!token) {
    return true;
  }

  const router = inject(Router);
  return router.createUrlTree(['/showroom']);
};
