import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { NotificationService } from '../notification.service';

export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const notifications = inject(NotificationService);
  return next(request).pipe(catchError((error: HttpErrorResponse) => {
    if (error.status === 0) notifications.showError('Cannot reach the server. Check that the API is running.');
    else if (error.status >= 500) notifications.showError('The server encountered an error. Please try again.');
    return throwError(() => error);
  }));
};
