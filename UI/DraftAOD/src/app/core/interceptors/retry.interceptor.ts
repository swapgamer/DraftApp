import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { retry, throwError, timer } from 'rxjs';

export const retryInterceptor: HttpInterceptorFn = (request, next) => {
  if (request.method !== 'GET') return next(request);

  return next(request).pipe(retry({
    count: 2,
    delay: (error: HttpErrorResponse, retryCount) => {
      const transient = error.status === 0 || error.status >= 500;
      return transient ? timer(retryCount * 350) : throwError(() => error);
    },
  }));
};
