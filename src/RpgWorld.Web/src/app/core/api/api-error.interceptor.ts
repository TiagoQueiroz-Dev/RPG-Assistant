import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

export class ApiRequestError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly correlationId?: string,
  ) {
    super(message);
    this.name = 'ApiRequestError';
  }
}

export const apiErrorInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }

      const message =
        typeof error.error?.message === 'string'
          ? error.error.message
          : 'Não foi possível concluir a comunicação com o mundo.';
      const correlationId = error.headers.get('x-correlation-id') ?? undefined;

      return throwError(() => new ApiRequestError(message, error.status, correlationId));
    }),
  );
