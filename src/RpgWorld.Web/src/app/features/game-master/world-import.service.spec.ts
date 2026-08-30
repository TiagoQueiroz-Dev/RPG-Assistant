import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';
import { WorldImportService } from './world-import.service';

describe('WorldImportService', () => {
  it('sends image, name and grid resolution as multipart data', () => {
    let submitted: FormData | undefined;
    const api = {
      post: vi.fn((path: string, body: FormData) => {
        submitted = body;
        return of({ path });
      }),
    };
    TestBed.configureTestingModule({ providers: [{ provide: ApiClient, useValue: api }] });
    const file = new File([new Uint8Array([1, 2, 3])], 'map.webp', { type: 'image/webp' });

    TestBed.inject(WorldImportService).import(file, 'Novo mundo', 24).subscribe();

    expect(api.post).toHaveBeenCalledWith('worlds/import', expect.any(FormData));
    const form = submitted!;
    expect(form.get('name')).toBe('Novo mundo');
    expect(form.get('gridResolution')).toBe('24');
    expect((form.get('file') as File).name).toBe('map.webp');
  });
});
