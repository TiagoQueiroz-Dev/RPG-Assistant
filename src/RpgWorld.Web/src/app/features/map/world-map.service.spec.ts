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

  it('loads only the selected analytical layer on demand', () => {
    const api = { get: vi.fn(() => of({})) };
    TestBed.configureTestingModule({ providers: [{ provide: ApiClient, useValue: api }] });

    TestBed.inject(WorldMapService).loadLayer('world-id', 'Population', 32, 16).subscribe();

    expect(api.get).toHaveBeenCalledWith(
      'worlds/world-id/map/layers/Population?minX=0&minY=0&maxX=31&maxY=15',
    );
  });

  it('loads the sanitized player map from its isolated endpoint', () => {
    const api = { get: vi.fn(() => of({})) };
    TestBed.configureTestingModule({ providers: [{ provide: ApiClient, useValue: api }] });

    TestBed.inject(WorldMapService).loadPlayer('world id', 'player id').subscribe();

    expect(api.get).toHaveBeenCalledWith('worlds/world%20id/players/player%20id/map');
  });
});
