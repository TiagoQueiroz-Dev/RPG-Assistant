import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';
import { WorldMapLayerMode, WorldMapLayerView, WorldMapView } from './models/world-map-view';

@Injectable({ providedIn: 'root' })
export class WorldMapService {
  private readonly api = inject(ApiClient);

  load(worldId: string): Observable<WorldMapView> {
    return this.api.get<WorldMapView>(`worlds/${encodeURIComponent(worldId)}/map`);
  }

  loadLayer(worldId: string, mode: WorldMapLayerMode, width: number, height: number): Observable<WorldMapLayerView> {
    return this.api.get<WorldMapLayerView>(
      `worlds/${encodeURIComponent(worldId)}/map/layers/${mode}?minX=0&minY=0&maxX=${width - 1}&maxY=${height - 1}`,
    );
  }
}
