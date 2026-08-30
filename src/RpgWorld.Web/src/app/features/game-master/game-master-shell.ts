import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { WorldMap } from '../map/world-map';
import { WorldAdminView } from './models/world-admin-view';

@Component({
  selector: 'app-game-master-shell',
  imports: [WorldMap],
  templateUrl: './game-master-shell.html',
  styleUrl: './game-master-shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GameMasterShell {
  protected readonly world = signal<WorldAdminView>({
    worldId: 'demo',
    name: 'As Marcas de Aster',
    currentTime: '17º dia da Lua Rubra · 19:40',
    simulationStatus: 'running',
    activeChunks: 12,
    totalActors: 100,
    cities: [
      { cityId: 'northwatch', name: 'Vigília do Norte', population: 1840, foodStockDays: 19 },
      { cityId: 'rivercross', name: 'Travessia do Rio', population: 970, foodStockDays: 8 },
    ],
    factions: [
      { factionId: 'crown', name: 'Coroa de Âmbar', power: 72, disposition: 'allied' },
      { factionId: 'veil', name: 'Conclave do Véu', power: 48, disposition: 'hostile' },
    ],
  });
}
