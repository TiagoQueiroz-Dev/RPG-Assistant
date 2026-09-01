import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';
import { PlayerWorldService } from './player-world.service';

describe('PlayerWorldService', () => {
  it('uses only the dedicated player view endpoint', () => {
    const api = { get: vi.fn(() => of({})) };
    TestBed.configureTestingModule({ providers: [{ provide: ApiClient, useValue: api }] });

    TestBed.inject(PlayerWorldService).load('world id', 'player id').subscribe();

    expect(api.get).toHaveBeenCalledWith('worlds/world%20id/players/player%20id/view');
  });

  it('uses the dedicated current region endpoint', () => {
    const api = { get: vi.fn(() => of({})) };
    TestBed.configureTestingModule({ providers: [{ provide: ApiClient, useValue: api }] });

    TestBed.inject(PlayerWorldService).loadCurrentRegion('world id', 'player id').subscribe();

    expect(api.get).toHaveBeenCalledWith(
      'worlds/world%20id/players/player%20id/current-region',
    );
  });
});
