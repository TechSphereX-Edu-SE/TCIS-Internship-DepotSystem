import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth-interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Route params and query params can be read as component inputs
    provideRouter(routes, withComponentInputBinding()),
    // Every API call goes through the interceptor that attaches the JWT
    provideHttpClient(withInterceptors([authInterceptor])),
  ],
};
