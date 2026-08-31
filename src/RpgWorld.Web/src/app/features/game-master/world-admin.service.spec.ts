import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';
import { WorldAdminService } from './world-admin.service';

describe('WorldAdminService', () => {
  it('loads a bounded entity page for the master inspector', () => {
    const api = { get: vi.fn(() => of({})) };
    TestBed.configureTestingModule({ providers: [{ provide: ApiClient, useValue: api }] });

    TestBed.inject(WorldAdminService).inspect('d2719f70-2235-4ad5-8fe7-18fd560a0036', 'npcs').subscribe();

    expect(api.get).toHaveBeenCalledWith(
      'worlds/d2719f70-2235-4ad5-8fe7-18fd560a0036/admin?entityType=npcs&page=1&pageSize=50',
    );
  });
});
