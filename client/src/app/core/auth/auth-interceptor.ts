import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth-service';

// Identity endpoints (login, refresh) must work without a token
const isPublicApi = (url: string) => url.startsWith('/api/identity/');

// Adds "Authorization: Bearer <token>" to API calls and sends the user to login when the API answers 401
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const token = auth.accessToken();

  const isApi = request.url.startsWith('/api/');
  const authorized =
    token && isApi && !isPublicApi(request.url)
      ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : request;

  return next(authorized).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && !isPublicApi(request.url)) {
        auth.logout(true);
      }
      return throwError(() => error);
    }),
  );
};
