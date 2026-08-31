import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';
import { GameMasterCommandService } from './game-master-command.service';

describe('GameMasterCommandService', () => {
  it('posts one typed command to the authenticated world endpoint', () => {
    const api = { post: vi.fn(() => of({})) };
    TestBed.configureTestingModule({ providers: [{ provide: ApiClient, useValue: api }] });
    const command = { action: 'MoveActor' as const, actorId: 'actor-1', x: 7, y: 9 };

    TestBed.inject(GameMasterCommandService).execute('world-1', command).subscribe();

    expect(api.post).toHaveBeenCalledWith('worlds/world-1/admin/commands', command);
  });
});
