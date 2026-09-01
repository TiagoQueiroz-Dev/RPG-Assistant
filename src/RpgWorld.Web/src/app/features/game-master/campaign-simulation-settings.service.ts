import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';

export interface CampaignSimulationSettings {
  readonly worldId: string;
  readonly npcDensity: number;
  readonly creatureSpawnRate: number;
  readonly warFrequency: number;
  readonly economicDifficulty: number;
  readonly resourceScarcity: number;
  readonly migrationRate: number;
  readonly populationGrowth: number;
  readonly simulationSpeed: number;
  readonly version: number;
  readonly updatedAtUtc: string;
}

export type CampaignSimulationSettingsInput = Omit<
  CampaignSimulationSettings,
  'worldId' | 'version' | 'updatedAtUtc'
>;

@Injectable({ providedIn: 'root' })
export class CampaignSimulationSettingsService {
  private readonly api = inject(ApiClient);

  get(worldId: string): Observable<CampaignSimulationSettings> {
    return this.api.get(`worlds/${encodeURIComponent(worldId)}/simulation/settings`);
  }

  update(worldId: string, settings: CampaignSimulationSettingsInput): Observable<CampaignSimulationSettings> {
    return this.api.put(`worlds/${encodeURIComponent(worldId)}/simulation/settings`, settings);
  }
}
