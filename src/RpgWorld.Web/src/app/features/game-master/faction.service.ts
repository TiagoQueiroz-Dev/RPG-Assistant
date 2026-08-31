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
  readonly state: 'Alliance' | 'Neutral' | 'Hostile' | 'War' | 'Vassal';
  readonly affinity: number;
  readonly tension: number;
  readonly isVassal: boolean;
  readonly updatedAtUtc: string;
  readonly lastWarScore: FactionWarScoreView | null;
  readonly warPreventedUntilUtc: string | null;
  readonly warPreventionReason: string | null;
  readonly history: readonly FactionRelationChangeView[];
}

export interface FactionWarScoreView {
  readonly factors: {
    readonly borderConflict: number;
    readonly resourceDispute: number;
    readonly historicalHatred: number;
    readonly aggressiveLeader: number;
    readonly weakEnemy: number;
  };
  readonly total: number;
  readonly declareWarThreshold: number;
  readonly evaluatedAtUtc: string;
  readonly reachedThreshold: boolean;
}

export interface FactionRelationChangeView {
  readonly changeId: string;
  readonly source: 'Event' | 'Border' | 'Trade' | 'Leadership' | 'History';
  readonly reason: string;
  readonly affinityDelta: number;
  readonly tensionDelta: number;
  readonly previousAffinity: number;
  readonly affinity: number;
  readonly previousTension: number;
  readonly tension: number;
  readonly previousState: 'Alliance' | 'Neutral' | 'Hostile' | 'War' | 'Vassal';
  readonly state: 'Alliance' | 'Neutral' | 'Hostile' | 'War' | 'Vassal';
  readonly sourceEventId: string | null;
  readonly occurredAtUtc: string;
}

@Injectable({ providedIn: 'root' })
export class FactionService {
  private readonly api = inject(ApiClient);

  list(worldId: string): Observable<readonly FactionMasterView[]> {
    return this.api.get<readonly FactionMasterView[]>(`worlds/${worldId}/factions`);
  }
}
