import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);

  get<T>(path: string, params?: HttpParams): Observable<T> {
    return this.http.get<T>(this.url(path), { params });
  }

  post<TResponse, TBody>(path: string, body: TBody): Observable<TResponse> {
    return this.http.post<TResponse>(this.url(path), body);
  }

  put<TResponse, TBody>(path: string, body: TBody): Observable<TResponse> {
    return this.http.put<TResponse>(this.url(path), body);
  }

  delete(path: string): Observable<void> {
    return this.http.delete<void>(this.url(path));
  }

  private url(path: string): string {
    const base = environment.apiBaseUrl.replace(/\/$/, '');
    const normalizedPath = path.replace(/^\//, '');
    return `${base}/${normalizedPath}`;
  }
}
