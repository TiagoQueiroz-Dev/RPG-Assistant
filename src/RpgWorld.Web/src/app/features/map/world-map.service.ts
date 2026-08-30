import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';
import { WorldMapView } from './models/world-map-view';

@Injectable({ providedIn: 'root' })
export class WorldMapService {
  private readonly api = inject(ApiClient);

  load(worldId: string): Observable<WorldMapView> {
    return this.api.get<WorldMapView>(`worlds/${encodeURIComponent(worldId)}/map`);
  }
}
