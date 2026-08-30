import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { WorldMap } from '../map/world-map';
import { WorldAdminView } from './models/world-admin-view';
import { WorldImportService } from './world-import.service';

@Component({
  selector: 'app-game-master-shell',
  imports: [WorldMap],
  templateUrl: './game-master-shell.html',
  styleUrl: './game-master-shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GameMasterShell {
  private readonly importer = inject(WorldImportService);

  protected readonly importState = signal<'idle' | 'uploading' | 'completed' | 'error'>('idle');
  protected readonly importMessage = signal('PNG, JPG ou WEBP · até 10 MB');

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

  protected importMap(event: SubmitEvent): void {
    event.preventDefault();
    const form = event.currentTarget as HTMLFormElement;
    const data = new FormData(form);
    const file = data.get('file');
    const name = data.get('name')?.toString().trim() ?? '';
    const gridResolution = Number(data.get('gridResolution'));

    if (!(file instanceof File) || file.size === 0 || !name) {
      this.importState.set('error');
      this.importMessage.set('Informe um nome e selecione uma imagem válida.');
      return;
    }

    this.importState.set('uploading');
    this.importMessage.set('Processando imagem e construindo o mundo…');
    this.importer.import(file, name, gridResolution).subscribe({
      next: result => {
        this.world.update(world => ({ ...world, worldId: result.worldId, name: result.name }));
        this.importState.set('completed');
        this.importMessage.set(
          `${result.tileCount} tiles em ${result.chunkCount} chunks · ${result.imageFormat.toUpperCase()}`,
        );
        form.reset();
      },
      error: () => {
        this.importState.set('error');
        this.importMessage.set('A importação falhou. Verifique o arquivo e tente novamente.');
      },
    });
  }
}
