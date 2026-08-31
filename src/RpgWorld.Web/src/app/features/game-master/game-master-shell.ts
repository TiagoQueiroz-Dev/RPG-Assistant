import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { WorldMap } from '../map/world-map';
import { WorldAdminView } from './models/world-admin-view';
import { WorldImportService } from './world-import.service';
import { WorldMapTileView } from '../map/models/world-map-view';
import { NpcInspectorService } from './npc-inspector.service';
import { ActorAtPositionView, NpcInspectorView, NpcTraitInspectorView } from './models/npc-inspector-view';
import { CityService } from './city.service';
import { FactionService } from './faction.service';

@Component({
  selector: 'app-game-master-shell',
  imports: [WorldMap],
  templateUrl: './game-master-shell.html',
  styleUrl: './game-master-shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GameMasterShell {
  private readonly importer = inject(WorldImportService);
  private readonly npcInspector = inject(NpcInspectorService);
  private readonly cities = inject(CityService);
  private readonly factions = inject(FactionService);

  protected readonly importState = signal<'idle' | 'uploading' | 'completed' | 'error'>('idle');
  protected readonly importMessage = signal('PNG, JPG ou WEBP · até 10 MB');
  protected readonly selectedMapTile = signal<WorldMapTileView | null>(null);
  protected readonly classificationRevision = signal(0);
  protected readonly selectedBrush = signal<string | null>(null);
  protected readonly brushSize = signal(1);
  protected readonly actorsAtSelectedTile = signal<readonly ActorAtPositionView[]>([]);
  protected readonly selectedNpc = signal<NpcInspectorView | null>(null);
  protected readonly npcInspectorState = signal<'idle' | 'loading' | 'loaded' | 'empty' | 'error'>('idle');

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
        this.loadCities(result.worldId);
        this.loadFactions(result.worldId);
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

  protected confirmBiome(event: SubmitEvent): void {
    event.preventDefault();
    const tile = this.selectedMapTile();
    const biome = new FormData(event.currentTarget as HTMLFormElement).get('biome')?.toString();
    if (!tile || !biome) return;
    this.importer.confirmBiome(this.world().worldId, tile, biome).subscribe(() => {
      this.selectedMapTile.set(null);
      this.classificationRevision.update(value => value + 1);
    });
  }

  protected reprocessClassification(): void {
    this.importer.reprocess(this.world().worldId).subscribe(() =>
      this.classificationRevision.update(value => value + 1));
  }

  protected handleMapTileSelected(tile: WorldMapTileView): void {
    this.selectedMapTile.set(tile);
    this.loadActorsAtTile(tile);
    const brush = this.selectedBrush();
    if (!brush) return;
    this.importer.paint(this.world().worldId, tile, brush, this.brushSize()).subscribe(() =>
      this.classificationRevision.update(value => value + 1));
  }

  protected undoMapEdit(): void {
    this.importer.undo(this.world().worldId).subscribe(() =>
      this.classificationRevision.update(value => value + 1));
  }

  protected redoMapEdit(): void {
    this.importer.redo(this.world().worldId).subscribe(() =>
      this.classificationRevision.update(value => value + 1));
  }

  protected inspectNpc(actorId: string): void {
    this.npcInspectorState.set('loading');
    this.npcInspector.inspect(actorId).subscribe({
      next: npc => {
        this.selectedNpc.set(npc);
        this.npcInspectorState.set('loaded');
      },
      error: () => {
        this.selectedNpc.set(null);
        this.npcInspectorState.set('error');
      },
    });
  }

  protected inspectNpcById(event: SubmitEvent): void {
    event.preventDefault();
    const actorId = new FormData(event.currentTarget as HTMLFormElement).get('actorId')?.toString().trim();
    if (actorId) this.inspectNpc(actorId);
  }

  protected modifierEntries(trait: NpcTraitInspectorView): readonly { action: string; multiplier: number }[] {
    return Object.entries(trait.actionScoreMultipliers)
      .map(([action, multiplier]) => ({ action, multiplier }))
      .sort((left, right) => left.action.localeCompare(right.action));
  }

  private loadActorsAtTile(tile: WorldMapTileView): void {
    const worldId = this.world().worldId;
    if (!this.isGuid(worldId)) {
      this.actorsAtSelectedTile.set([]);
      this.selectedNpc.set(null);
      this.npcInspectorState.set('empty');
      return;
    }
    this.npcInspectorState.set('loading');
    this.npcInspector.listAtPosition(worldId, tile.x, tile.y).subscribe({
      next: actors => {
        this.actorsAtSelectedTile.set(actors);
        const firstNpc = actors.find(actor => actor.kind === 'npc');
        if (firstNpc) this.inspectNpc(firstNpc.actorId);
        else {
          this.selectedNpc.set(null);
          this.npcInspectorState.set('empty');
        }
      },
      error: () => {
        this.actorsAtSelectedTile.set([]);
        this.selectedNpc.set(null);
        this.npcInspectorState.set('error');
      },
    });
  }

  private isGuid(value: string): boolean {
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
  }

  private loadCities(worldId: string): void {
    this.cities.list(worldId).subscribe({
      next: cities => this.world.update(world => ({
        ...world,
        cities: cities.map(city => ({
          cityId: city.cityId,
          name: city.name,
          population: city.population,
          foodStockDays: Math.floor((city.resourceStocks['food'] ?? 0) / Math.max(1, city.population)),
          status: city.status,
          wealth: city.wealth,
          foodPrice: city.resourceMarkets['food']?.unitPrice,
          foodMarketCondition: city.resourceMarkets['food']?.condition,
        })),
      })),
      error: () => this.world.update(world => ({ ...world, cities: [] })),
    });
  }

  private loadFactions(worldId: string): void {
    this.factions.list(worldId).subscribe({
      next: factions => this.world.update(world => ({
        ...world,
        factions: factions.map(faction => ({
          factionId: faction.factionId,
          name: faction.name,
          power: faction.militaryPower,
          disposition: 'neutral',
          type: faction.type,
          status: faction.status,
          wealth: faction.wealth,
          memberCount: faction.memberActorIds.length,
          territorySize: faction.territory.length,
          diplomacySummary: faction.relations.length > 0
            ? faction.relations.map(relation => relation.lastWarScore
              ? `${relation.state} (guerra ${relation.lastWarScore.total}/${relation.lastWarScore.declareWarThreshold}${relation.warPreventedUntilUtc ? ', impedida' : ''})`
              : relation.state).join(', ')
            : 'sem relações',
        })),
      })),
      error: () => this.world.update(world => ({ ...world, factions: [] })),
    });
  }
}
