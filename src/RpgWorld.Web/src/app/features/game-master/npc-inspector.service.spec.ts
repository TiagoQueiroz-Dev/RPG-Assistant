import { TestBed } from '@angular/core/testing';
import { HttpParams } from '@angular/common/http';
import { of } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';
import { NpcInspectorService } from './npc-inspector.service';

describe('NpcInspectorService', () => {
  it('queries actors by tile and loads the selected NPC inspector', () => {
    let capturedParams: HttpParams | undefined;
    const api = {
      get: vi.fn((path: string, params?: HttpParams) => {
        capturedParams = params;
        return of({ path });
      }),
    };
    TestBed.configureTestingModule({ providers: [{ provide: ApiClient, useValue: api }] });
    const service = TestBed.inject(NpcInspectorService);

    service.listAtPosition('d2719f70-2235-4ad5-8fe7-18fd560a0036', 7, 9).subscribe();

    expect(api.get).toHaveBeenNthCalledWith(
      1,
      'worlds/d2719f70-2235-4ad5-8fe7-18fd560a0036/actors',
      expect.objectContaining({}),
    );
    expect(capturedParams?.get('x')).toBe('7');
    expect(capturedParams?.get('y')).toBe('9');

    service.inspect('8cc3b32f-c6e7-4514-be2b-ed62b0682358').subscribe();
    expect(api.get).toHaveBeenNthCalledWith(
      2,
      'actors/8cc3b32f-c6e7-4514-be2b-ed62b0682358/inspector',
    );
  });
});
