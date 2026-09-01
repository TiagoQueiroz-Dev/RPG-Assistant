import { ChangeDetectionStrategy, Component, inject, OnDestroy, signal } from '@angular/core';
import { WorldMap } from '../map/world-map';
import { PlayerWorldView } from './models/player-world-view';
import { WorldRealtimeService } from '../../core/realtime/world-realtime.service';
import { PlayerWorldService, ServerPlayerCurrentRegion } from './player-world.service';
import { WorldMapOverlay } from '../map/models/world-map-view';

@Component({
  selector: 'app-player-shell',
  imports: [WorldMap],
  templateUrl: './player-shell.html',
  styleUrl: './player-shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayerShell implements OnDestroy {
  private readonly realtime = inject(WorldRealtimeService);
  private readonly playerWorld = inject(PlayerWorldService);
  private stopRealtimeUpdates?: () => void;
  protected readonly visibilityRevision = signal(0);
  protected readonly visibleOverlays = signal<readonly WorldMapOverlay[]>([]);
  protected readonly currentRegion = signal<ServerPlayerCurrentRegion | null>(null);
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
      if (message.worldId === this.view().worldId) {
        this.visibilityRevision.update(value => value + 1);
        this.loadVisibleWorld();
      }
    });
    void this.realtime.joinPlayer(playerId).catch(() => undefined);
    this.loadVisibleWorld();
  }

  ngOnDestroy(): void {
    this.stopRealtimeUpdates?.();
    void this.realtime.leavePlayer(this.view().playerActorId);
  }

  private loadVisibleWorld(): void {
    const current = this.view();
    if (!/^[0-9a-f-]{36}$/i.test(current.worldId)) return;
    this.playerWorld.loadCurrentRegion(current.worldId, current.playerActorId).subscribe(server => {
      this.currentRegion.set(server);
      this.view.update(view => ({
        ...view,
        worldName: server.worldName,
        characterName: server.characterName,
        currentLocation: {
          ...view.currentLocation,
          name: server.regionName,
          description: `${server.regionKind === 'city' ? 'Cidade' : 'Região'} em X ${server.x} · Y ${server.y}; visão de ${server.perceptionRadius} tiles.`,
        },
        nearbyEntities: server.visibleEntities.map(entity => ({
          entityId: entity.id,
          displayName: entity.name,
          kind: entity.kind === 'creature' ? 'creature' : 'person',
          category: entity.category,
          distance: `${entity.distance} tile(s)`,
        })),
      }));
      this.visibleOverlays.set([
        ...server.visibleEntities.map(entity => ({
          id: entity.id, x: entity.x, y: entity.y, kind: 'entity' as const, label: entity.name,
        })),
        ...server.visibleEstablishments.map(structure => ({
          id: structure.id, x: structure.x, y: structure.y, kind: 'structure' as const, label: structure.kind,
        })),
      ]);
    });
  }
}
