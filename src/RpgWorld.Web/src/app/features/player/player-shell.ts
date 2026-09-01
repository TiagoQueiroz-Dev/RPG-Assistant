import { ChangeDetectionStrategy, Component, inject, OnDestroy, signal } from '@angular/core';
import { WorldMap } from '../map/world-map';
import { PlayerWorldView } from './models/player-world-view';
import { WorldRealtimeService } from '../../core/realtime/world-realtime.service';

@Component({
  selector: 'app-player-shell',
  imports: [WorldMap],
  templateUrl: './player-shell.html',
  styleUrl: './player-shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayerShell implements OnDestroy {
  private readonly realtime = inject(WorldRealtimeService);
  private stopRealtimeUpdates?: () => void;
  protected readonly visibilityRevision = signal(0);
  protected readonly view = signal<PlayerWorldView>({
    playerActorId: '00000000-0000-4000-8000-000000000001',
    worldId: 'demo',
    worldName: 'As Marcas de Aster',
    characterName: 'Liora Venn',
    localTime: 'Anoitecer · 19:40',
    currentLocation: {
      name: 'Estrada de Salgueiro',
      description: 'A trilha desce entre pinheiros e ruínas cobertas por hera.',
      weather: 'Névoa leve, 12°C',
    },
    nearbyEntities: [
      {
        entityId: 'lantern-keeper',
        displayName: 'Guardião da Lanterna',
        kind: 'person',
        distance: '40 m',
      },
      {
        entityId: 'old-arch',
        displayName: 'Arco de pedra antigo',
        kind: 'landmark',
        distance: '120 m',
      },
    ],
    discoveredRegions: [
      { regionId: 'willow-road', name: 'Estrada de Salgueiro', knowledge: 'visible' },
      { regionId: 'northwatch', name: 'Vigília do Norte', knowledge: 'known' },
      { regionId: 'white-woods', name: 'Bosque Branco', knowledge: 'discovered' },
    ],
  });

  constructor() {
    const playerId = this.view().playerActorId;
    this.stopRealtimeUpdates = this.realtime.onWorldUpdated(message => {
      if (message.worldId === this.view().worldId)
        this.visibilityRevision.update(value => value + 1);
    });
    void this.realtime.joinPlayer(playerId).catch(() => undefined);
  }

  ngOnDestroy(): void {
    this.stopRealtimeUpdates?.();
    void this.realtime.leavePlayer(this.view().playerActorId);
  }
}
