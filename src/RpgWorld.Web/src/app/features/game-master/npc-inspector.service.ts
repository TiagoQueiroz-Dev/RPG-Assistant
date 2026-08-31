import { HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';
import { ActorAtPositionView, NpcInspectorView } from './models/npc-inspector-view';

@Injectable({ providedIn: 'root' })
export class NpcInspectorService {
  private readonly api = inject(ApiClient);

  listAtPosition(worldId: string, x: number, y: number): Observable<readonly ActorAtPositionView[]> {
    const params = new HttpParams().set('x', x).set('y', y);
    return this.api.get<readonly ActorAtPositionView[]>(`worlds/${worldId}/actors`, params);
  }

  inspect(actorId: string): Observable<NpcInspectorView> {
    return this.api.get<NpcInspectorView>(`actors/${actorId}/inspector`);
  }
}
