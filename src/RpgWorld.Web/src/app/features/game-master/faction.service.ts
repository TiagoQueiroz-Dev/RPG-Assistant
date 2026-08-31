import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';

export interface FactionMasterView {
  readonly factionId: string;
  readonly worldId: string;
  readonly name: string;
  readonly type: 'Kingdom' | 'Guild' | 'Cult' | 'BanditGroup' | 'Tribe' | 'Army' | 'MerchantGuild';
  readonly status: 'Active' | 'Dissolved';
  readonly leaderActorId: string | null;
  readonly memberActorIds: readonly string[];
  readonly controlledCityIds: readonly string[];
  readonly territory: readonly { x: number; y: number }[];
  readonly wealth: number;
  readonly militaryPower: number;
  readonly relations: readonly FactionRelationView[];
}

export interface FactionRelationView {
  readonly targetFactionId: string;
  readonly kind: 'Neutral' | 'Allied' | 'Hostile';
  readonly score: number;
  readonly updatedAtUtc: string;
}

@Injectable({ providedIn: 'root' })
export class FactionService {
  private readonly api = inject(ApiClient);

  list(worldId: string): Observable<readonly FactionMasterView[]> {
    return this.api.get<readonly FactionMasterView[]>(`worlds/${worldId}/factions`);
  }
}
