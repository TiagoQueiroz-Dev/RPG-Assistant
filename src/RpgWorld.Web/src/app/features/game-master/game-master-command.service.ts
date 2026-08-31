import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';

export type GameMasterCommandType =
  | 'CreateNpc' | 'DeleteNpc' | 'MoveActor' | 'CreateCity' | 'DestroyCity'
  | 'AdjustResource' | 'CreateCreature' | 'ChangeClimate' | 'CreateEvent'
  | 'DeclareWar' | 'EndWar' | 'ChangeFactionRelation';

export interface GameMasterCommand {
  readonly action: GameMasterCommandType;
  readonly actorId?: string;
  readonly cityId?: string;
  readonly resourceDepositId?: string;
  readonly factionId?: string;
  readonly targetFactionId?: string;
  readonly name?: string;
  readonly reason?: string;
  readonly x?: number;
  readonly y?: number;
  readonly maximumHealth?: number;
  readonly initialPopulation?: number;
  readonly initialWealth?: number;
  readonly territory?: readonly { readonly x: number; readonly y: number }[];
  readonly resourceQuantityDelta?: number;
  readonly temperatureCelsius?: number;
  readonly humidity?: number;
  readonly eventType?: string;
  readonly eventPayload?: string;
  readonly affinityDelta?: number;
  readonly tensionDelta?: number;
  readonly vassalage?: boolean;
}

export interface GameMasterCommandResult {
  readonly commandId: string;
  readonly worldId: string;
  readonly command: string;
  readonly entityId: string | null;
  readonly occurredAtUtc: string;
  readonly summary: string;
}

@Injectable({ providedIn: 'root' })
export class GameMasterCommandService {
  private readonly api = inject(ApiClient);

  execute(worldId: string, command: GameMasterCommand): Observable<GameMasterCommandResult> {
    return this.api.post<GameMasterCommandResult, GameMasterCommand>(
      `worlds/${worldId}/admin/commands`, command,
    );
  }
}
