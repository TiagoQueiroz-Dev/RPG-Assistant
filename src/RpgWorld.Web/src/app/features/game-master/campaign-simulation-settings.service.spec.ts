import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';
import { CampaignSimulationSettingsService } from './campaign-simulation-settings.service';

describe('CampaignSimulationSettingsService', () => {
  it('reads and updates campaign-scoped effective settings', () => {
    const api = { get: vi.fn(() => of({})), put: vi.fn(() => of({})) };
    TestBed.configureTestingModule({ providers: [{ provide: ApiClient, useValue: api }] });
    const service = TestBed.inject(CampaignSimulationSettingsService);
    const settings = {
      npcDensity: 0.5, creatureSpawnRate: 2, warFrequency: 3, economicDifficulty: 1.5,
      resourceScarcity: 2, migrationRate: 0.8, populationGrowth: 1.2, simulationSpeed: 4,
    };

    service.get('world 1').subscribe();
    service.update('world 1', settings).subscribe();

    expect(api.get).toHaveBeenCalledWith('worlds/world%201/simulation/settings');
    expect(api.put).toHaveBeenCalledWith('worlds/world%201/simulation/settings', settings);
  });
});
