import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';

export type CustomContentKind = 'Creature' | 'Item' | 'Npc' | 'Biome' | 'Rule' | 'Class' | 'Faction' | 'Event';

export interface CustomContentDefinition {
  readonly id: string;
  readonly worldId: string;
  readonly kind: CustomContentKind;
  readonly code: string;
  readonly name: string;
  readonly payload: string;
  readonly version: number;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string;
}

export interface CustomContentInput {
  readonly kind: CustomContentKind;
  readonly code: string;
  readonly name: string;
  readonly payload: Readonly<Record<string, unknown>>;
}

@Injectable({ providedIn: 'root' })
export class CustomContentService {
  private readonly api = inject(ApiClient);

  list(worldId: string): Observable<readonly CustomContentDefinition[]> {
    return this.api.get(`worlds/${encodeURIComponent(worldId)}/custom-content`);
  }

  create(worldId: string, input: CustomContentInput): Observable<CustomContentDefinition> {
    return this.api.post(`worlds/${encodeURIComponent(worldId)}/custom-content`, input);
  }

  update(
    worldId: string,
    definitionId: string,
    input: Pick<CustomContentInput, 'name' | 'payload'>,
  ): Observable<CustomContentDefinition> {
    return this.api.put(
      `worlds/${encodeURIComponent(worldId)}/custom-content/${encodeURIComponent(definitionId)}`,
      input,
    );
  }

  delete(worldId: string, definitionId: string): Observable<void> {
    return this.api.delete(
      `worlds/${encodeURIComponent(worldId)}/custom-content/${encodeURIComponent(definitionId)}`,
    );
  }
}
