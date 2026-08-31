import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';

export interface WorldInspectorEntity {
  readonly id: string;
  readonly entityType: string;
  readonly name: string;
  readonly status: string;
  readonly x: number | null;
  readonly y: number | null;
  readonly regionX: number | null;
  readonly regionY: number | null;
  readonly factionId: string | null;
  readonly detailPath: string | null;
  readonly metrics: Readonly<Record<string, string>>;
}

export interface WorldInspectorView {
  readonly worldId: string;
  readonly name: string;
  readonly isSimulationRunning: boolean;
  readonly currentInstant: string | null;
  readonly map: { readonly width: number; readonly height: number; readonly chunkSize: number; readonly totalTiles: number; readonly totalChunks: number };
  readonly summary: {
    readonly totalActors: number;
    readonly npcs: number;
    readonly players: number;
    readonly creatures: number;
    readonly activeChunks: number;
    readonly resourceDeposits: number;
    readonly availableResourceQuantity: number;
    readonly cities: number;
    readonly totalPopulation: number;
    readonly cityWealth: number;
    readonly factions: number;
    readonly armies: number;
    readonly militaryPower: number;
    readonly diplomaticRelations: number;
    readonly activeWars: number;
  };
  readonly entityType: string;
  readonly entities: readonly WorldInspectorEntity[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalEntityCount: number;
  readonly totalPages: number;
  readonly availableEntityTypes: readonly string[];
}

@Injectable({ providedIn: 'root' })
export class WorldAdminService {
  private readonly api = inject(ApiClient);

  inspect(
    worldId: string,
    entityType = 'chunks',
    page = 1,
    pageSize = 50,
    filters?: { readonly regionX?: number; readonly regionY?: number; readonly factionId?: string },
  ): Observable<WorldInspectorView> {
    const suffix = [
      filters?.regionX === undefined ? null : `regionX=${filters.regionX}`,
      filters?.regionY === undefined ? null : `regionY=${filters.regionY}`,
      filters?.factionId ? `factionId=${encodeURIComponent(filters.factionId)}` : null,
    ].filter(value => value !== null).join('&');
    return this.api.get<WorldInspectorView>(
      `worlds/${worldId}/admin?entityType=${encodeURIComponent(entityType)}&page=${page}&pageSize=${pageSize}${suffix ? `&${suffix}` : ''}`,
    );
  }
}
