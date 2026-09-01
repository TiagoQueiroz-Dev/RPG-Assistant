import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';

export interface ServerPlayerWorldView {
  readonly playerActorId: string;
  readonly worldId: string;
  readonly worldName: string;
  readonly characterName: string;
  readonly x: number;
  readonly y: number;
  readonly perceptionRadius: number;
  readonly visibleEntities: readonly {
    readonly id: string;
    readonly name: string;
    readonly kind: 'npc' | 'player' | 'creature';
    readonly x: number;
    readonly y: number;
    readonly distance: number;
  }[];
  readonly visibleStructures: readonly {
    readonly id: string;
    readonly kind: string;
    readonly x: number;
    readonly y: number;
  }[];
  readonly relevantEvents: readonly {
    readonly id: string;
    readonly type: string;
    readonly timestampUtc: string;
    readonly x: number | null;
    readonly y: number | null;
  }[];
}

@Injectable({ providedIn: 'root' })
export class PlayerWorldService {
  private readonly api = inject(ApiClient);

  load(worldId: string, playerActorId: string): Observable<ServerPlayerWorldView> {
    return this.api.get<ServerPlayerWorldView>(
      `worlds/${encodeURIComponent(worldId)}/players/${encodeURIComponent(playerActorId)}/view`,
    );
  }
}
