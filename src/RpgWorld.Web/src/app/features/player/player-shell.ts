import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { PlayerWorldView } from './models/player-world-view';

@Component({
  selector: 'app-player-shell',
  templateUrl: './player-shell.html',
  styleUrl: './player-shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlayerShell {
  protected readonly view = signal<PlayerWorldView>({
    worldId: 'demo-world',
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
}
