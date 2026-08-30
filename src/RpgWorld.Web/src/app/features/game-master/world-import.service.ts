import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';
import { WorldImportResult } from './models/world-import-result';
import { WorldMapTileView } from '../map/models/world-map-view';

@Injectable({ providedIn: 'root' })
export class WorldImportService {
  private readonly api = inject(ApiClient);

  import(file: File, name: string, gridResolution: number): Observable<WorldImportResult> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('name', name);
    form.append('gridResolution', gridResolution.toString());
    return this.api.post<WorldImportResult, FormData>('worlds/import', form);
  }

  confirmBiome(worldId: string, tile: WorldMapTileView, biomeCode: string): Observable<void> {
    return this.api.put<void, { biomeCode: string }>(
      `worlds/${worldId}/tiles/${tile.x}/${tile.y}/biome`, { biomeCode });
  }

  reprocess(worldId: string): Observable<unknown> {
    return this.api.post<unknown, Record<string, never>>(
      `worlds/${worldId}/classification/reprocess`, {});
  }

  paint(
    worldId: string,
    tile: WorldMapTileView,
    brush: string,
    size: number,
  ): Observable<unknown> {
    return this.api.post<unknown, object>(`worlds/${worldId}/map/paint`, {
      brush,
      centerX: tile.x,
      centerY: tile.y,
      size,
    });
  }

  undo(worldId: string): Observable<unknown> {
    return this.api.post<unknown, Record<string, never>>(`worlds/${worldId}/map/undo`, {});
  }

  redo(worldId: string): Observable<unknown> {
    return this.api.post<unknown, Record<string, never>>(`worlds/${worldId}/map/redo`, {});
  }
}
