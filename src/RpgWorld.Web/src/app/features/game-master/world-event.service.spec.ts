import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';
import { WorldEventService } from './world-event.service';

describe('WorldEventService', () => {
  it('loads the newest master timeline page', () => {
    const api = { get: vi.fn(() => of({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 })) };
    TestBed.configureTestingModule({ providers: [{ provide: ApiClient, useValue: api }] });

    TestBed.inject(WorldEventService).list('d2719f70-2235-4ad5-8fe7-18fd560a0036').subscribe();

    expect(api.get).toHaveBeenCalledWith(
      'worlds/d2719f70-2235-4ad5-8fe7-18fd560a0036/events?page=1&pageSize=20&sort=NewestFirst',
    );
  });
});
