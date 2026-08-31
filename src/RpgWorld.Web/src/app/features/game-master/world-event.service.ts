import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';

export interface WorldEventTimelineItem {
  readonly id: string;
  readonly worldId: string;
  readonly type: string;
  readonly timestampUtc: string;
  readonly position: { readonly x: number; readonly y: number } | null;
  readonly actors: readonly string[];
  readonly payload: Readonly<Record<string, unknown>>;
  readonly payloadVersion: number;
}

export interface WorldEventTimelinePage {
  readonly items: readonly WorldEventTimelineItem[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
}

@Injectable({ providedIn: 'root' })
export class WorldEventService {
  private readonly api = inject(ApiClient);

  list(worldId: string, page = 1, pageSize = 20): Observable<WorldEventTimelinePage> {
    return this.api.get<WorldEventTimelinePage>(
      `worlds/${worldId}/events?page=${page}&pageSize=${pageSize}&sort=NewestFirst`,
    );
  }
}
