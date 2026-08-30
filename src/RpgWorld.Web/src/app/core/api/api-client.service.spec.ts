import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../../environments/environment';
import { ApiRequestError, apiErrorInterceptor } from './api-error.interceptor';
import { ApiClient } from './api-client.service';

describe('ApiClient', () => {
  let client: ApiClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ApiClient,
        provideHttpClient(withInterceptors([apiErrorInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    client = TestBed.inject(ApiClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('centralizes the configured API base URL', () => {
    client.get<{ name: string }>('worlds/current').subscribe((result) => {
      expect(result.name).toBe('Aster');
    });

    const request = http.expectOne(`${environment.apiBaseUrl}/worlds/current`);
    request.flush({ name: 'Aster' });
  });

  it('normalizes HTTP failures into ApiRequestError', () => {
    client.get('worlds/current').subscribe({
      error: (error: unknown) => {
        expect(error).toBeInstanceOf(ApiRequestError);
        expect((error as ApiRequestError).status).toBe(503);
      },
    });

    http
      .expectOne(`${environment.apiBaseUrl}/worlds/current`)
      .flush(
        { message: 'Mundo temporariamente indisponível.' },
        { status: 503, statusText: 'Unavailable' },
      );
  });
});
