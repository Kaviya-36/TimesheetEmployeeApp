import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, finalize, throwError } from 'rxjs';
import { AuthService }    from '../services/auth.service';
import { LoadingService } from '../services/loading.service';
import { ToastService }   from '../services/toast.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth    = inject(AuthService);
  const loading = inject(LoadingService);
  const toast   = inject(ToastService);

  loading.show();

  const token   = auth.token();
  const authReq = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authReq).pipe(
    catchError((err: HttpErrorResponse) => {
      switch (err.status) {
        case 401:
          toast.error('Session Expired', 'Please login again.');
          auth.logout();
          break;
        case 403:
          toast.error('Access Denied', 'You do not have permission for this action.');
          break;
        case 500:
          toast.error('Server Error', 'Something went wrong. Please try again.');
          break;
      }
      return throwError(() => err);
    }),
    finalize(() => loading.hide())
  );
};
