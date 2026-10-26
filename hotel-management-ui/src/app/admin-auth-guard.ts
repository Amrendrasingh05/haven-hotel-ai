
import { inject } from '@angular/core';
import {
  CanActivateFn,
  Router
} from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { catchError, map, of } from 'rxjs';

export const adminAuthGuard: CanActivateFn = () => {
  const http = inject(HttpClient);
  const router = inject(Router);

  const token = sessionStorage.getItem('havenAdminSessionToken');

  if (!token) {
    return router.createUrlTree(['/admin-login']);
  }

  return http.get<unknown>(
    'https://haven-hotel-api.onrender.com/api/service-requests',
    {
      headers: {
        'X-Admin-Session': token
      }
    }
  ).pipe(
    map(() => true),
    catchError(() => {
      sessionStorage.removeItem('havenAdminSessionToken');
      sessionStorage.removeItem('havenAdminUsername');

      return of(router.createUrlTree(['/admin-login']));
    })
  );
};
