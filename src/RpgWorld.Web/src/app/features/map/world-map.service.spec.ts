import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';
import { WorldMapService } from './world-map.service';

describe('WorldMapService', () => {
  it('loads the selected world from the backend map endpoint', () => {
    const api = { get: vi.fn(() => of({})) };
    TestBed.configureTestingModule({ providers: [{ provide: ApiClient, useValue: api }] });

    TestBed.inject(WorldMapService).load('demo world').subscribe();

    expect(api.get).toHaveBeenCalledWith('worlds/demo%20world/map');
  });
});
