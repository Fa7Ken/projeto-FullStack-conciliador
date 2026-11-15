import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const LOGIN_URL = '/api/auth/login';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const isApiCall = request.url.startsWith('/api/');
  const isLogin = request.url === LOGIN_URL;
  const token = isApiCall && !isLogin ? auth.token() : null;

  const outgoing = token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;

  return next(outgoing).pipe(
    catchError((error: unknown) => {
      // Sessao recusada pela API: volta para o login
      if (error instanceof HttpErrorResponse && error.status === 401 && isApiCall && !isLogin) {
        auth.logout();
        void router.navigate(['/login']);
      }

      return throwError(() => error);
    }),
  );
};
