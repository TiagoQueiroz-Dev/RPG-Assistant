import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';
import { CustomContentService } from './custom-content.service';

describe('CustomContentService', () => {
  it('uses campaign-scoped CRUD endpoints', () => {
    const api = {
      get: vi.fn(() => of([])), post: vi.fn(() => of({})),
      put: vi.fn(() => of({})), delete: vi.fn(() => of(undefined)),
    };
    TestBed.configureTestingModule({ providers: [{ provide: ApiClient, useValue: api }] });
    const service = TestBed.inject(CustomContentService);
    const input = { kind: 'Creature' as const, code: 'owlbear', name: 'Owlbear', payload: { maximumHealth: 80 } };

    service.list('world 1').subscribe();
    service.create('world 1', input).subscribe();
    service.update('world 1', 'definition 1', { name: 'Elder Owlbear', payload: { maximumHealth: 120 } }).subscribe();
    service.delete('world 1', 'definition 1').subscribe();

    expect(api.get).toHaveBeenCalledWith('worlds/world%201/custom-content');
    expect(api.post).toHaveBeenCalledWith('worlds/world%201/custom-content', input);
    expect(api.put).toHaveBeenCalledWith('worlds/world%201/custom-content/definition%201', {
      name: 'Elder Owlbear', payload: { maximumHealth: 120 },
    });
    expect(api.delete).toHaveBeenCalledWith('worlds/world%201/custom-content/definition%201');
  });
});
