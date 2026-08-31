import { DecimalPipe } from '@angular/common';
import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { Subscription } from 'rxjs';
import {
  MapViewportTransform,
  screenToWorld,
  worldToScreen,
  zoomAt,
} from './map-transform';
import { WorldMapOverlay, WorldMapTileView, WorldMapView } from './models/world-map-view';
import { WorldMapService } from './world-map.service';

interface DragState {
  readonly pointerId: number;
  readonly startX: number;
  readonly startY: number;
  readonly offsetX: number;
  readonly offsetY: number;
  moved: boolean;
}

const TILE_SIZE = 18;
const BIOME_COLORS: Readonly<Record<string, string>> = {
  forest: '#2f6b4f',
  desert: '#c6a45e',
  grassland: '#77965a',
  mountain: '#73777b',
  swamp: '#456b61',
  snow: '#d9e4e5',
  ocean: '#315f7d',
  river: '#4c8faf',
  volcanic: '#873f32',
};

@Component({
  selector: 'app-world-map',
  imports: [DecimalPipe],
  templateUrl: './world-map.html',
  styleUrl: './world-map.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorldMap {
  readonly worldId = input('demo');
  readonly overlays = input<readonly WorldMapOverlay[]>([]);
  readonly refreshToken = input(0);
  readonly tileSelected = output<WorldMapTileView>();

  private readonly mapService = inject(WorldMapService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  protected readonly map = signal<WorldMapView | null>(null);
  private readonly transform = signal<MapViewportTransform>({
    offsetX: 0,
    offsetY: 0,
    scale: 1,
    tileSize: TILE_SIZE,
  });
  private readonly tileIndex = new Map<string, WorldMapTileView>();
  private resizeObserver?: ResizeObserver;
  private mapSubscription?: Subscription;
  private drag?: DragState;
  private ready = false;
  private renderedWorldId?: string;
  private frameRequested = false;

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly selectedTile = signal<WorldMapTileView | null>(null);
  protected readonly hoveredPosition = signal<{ x: number; y: number } | null>(null);
  protected readonly zoomPercent = computed(() => Math.round(this.transform().scale * 100));
  protected readonly legend = Object.entries(BIOME_COLORS).map(([code, color]) => ({ code, color }));

  constructor() {
    afterNextRender(() => this.initializeCanvas());

    effect(() => {
      const worldId = this.worldId();
      this.refreshToken();
      this.overlays();

      if (this.ready) {
        this.loadMap(worldId);
      }

      this.scheduleDraw();
    });

    this.destroyRef.onDestroy(() => {
      this.resizeObserver?.disconnect();
      this.mapSubscription?.unsubscribe();
    });
  }

  protected onPointerDown(event: PointerEvent): void {
    if (event.button !== 0) {
      return;
    }

    const canvas = this.canvas().nativeElement;
    canvas.setPointerCapture(event.pointerId);
    this.drag = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      offsetX: this.transform().offsetX,
      offsetY: this.transform().offsetY,
      moved: false,
    };
  }

  protected onPointerMove(event: PointerEvent): void {
    this.updateHoveredPosition(event);

    if (!this.drag || this.drag.pointerId !== event.pointerId) {
      return;
    }

    const deltaX = event.clientX - this.drag.startX;
    const deltaY = event.clientY - this.drag.startY;
    this.drag.moved ||= Math.hypot(deltaX, deltaY) > 4;
    this.transform.update(transform => ({
      ...transform,
      offsetX: this.drag!.offsetX + deltaX,
      offsetY: this.drag!.offsetY + deltaY,
    }));
    this.scheduleDraw();
  }

  protected onPointerUp(event: PointerEvent): void {
    if (!this.drag || this.drag.pointerId !== event.pointerId) {
      return;
    }

    const canvas = this.canvas().nativeElement;
    const wasMoved = this.drag.moved;
    this.drag = undefined;
    canvas.releasePointerCapture(event.pointerId);

    if (!wasMoved) {
      const position = screenToWorld(this.eventPoint(event), this.transform());
      const tile = this.tileIndex.get(this.tileKey(position.x, position.y));

      if (tile) {
        this.selectedTile.set(tile);
        this.tileSelected.emit(tile);
        this.scheduleDraw();
      }
    }
  }

  protected onPointerLeave(): void {
    if (!this.drag) {
      this.hoveredPosition.set(null);
    }
  }

  protected onWheel(event: WheelEvent): void {
    event.preventDefault();
    const factor = event.deltaY < 0 ? 1.12 : 1 / 1.12;
    this.transform.set(zoomAt(this.transform(), this.eventPoint(event), factor));
    this.updateHoveredPosition(event);
    this.scheduleDraw();
  }

  protected zoomBy(factor: number): void {
    const canvas = this.canvas().nativeElement;
    const anchor = { x: canvas.clientWidth / 2, y: canvas.clientHeight / 2 };
    this.transform.set(zoomAt(this.transform(), anchor, factor));
    this.scheduleDraw();
  }

  protected resetView(): void {
    this.fitMap();
  }

  protected onKeyDown(event: KeyboardEvent): void {
    const panDistance = 36;
    const movement: Readonly<Record<string, readonly [number, number]>> = {
      ArrowLeft: [panDistance, 0],
      ArrowRight: [-panDistance, 0],
      ArrowUp: [0, panDistance],
      ArrowDown: [0, -panDistance],
    };
    const delta = movement[event.key];

    if (delta) {
      event.preventDefault();
      this.transform.update(transform => ({
        ...transform,
        offsetX: transform.offsetX + delta[0],
        offsetY: transform.offsetY + delta[1],
      }));
      this.scheduleDraw();
      return;
    }

    if (event.key === '+' || event.key === '=') {
      event.preventDefault();
      this.zoomBy(1.12);
    } else if (event.key === '-') {
      event.preventDefault();
      this.zoomBy(1 / 1.12);
    }
  }

  private initializeCanvas(): void {
    const canvas = this.canvas().nativeElement;
    this.resizeObserver = new ResizeObserver(() => this.resizeCanvas());
    this.resizeObserver.observe(canvas.parentElement ?? canvas);
    this.resizeCanvas();
    this.ready = true;
    this.loadMap(this.worldId());
  }

  private loadMap(worldId: string): void {
    this.renderedWorldId = worldId;
    this.mapSubscription?.unsubscribe();
    this.loading.set(true);
    this.error.set(null);

    this.mapSubscription = this.mapService.load(worldId).subscribe({
      next: map => {
        this.map.set(map);
        this.tileIndex.clear();

        for (const chunk of map.chunks) {
          for (const tile of chunk.tiles) {
            this.tileIndex.set(this.tileKey(tile.x, tile.y), tile);
          }
        }

        this.loading.set(false);
        this.fitMap();
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Não foi possível carregar o mapa deste mundo.');
        this.scheduleDraw();
      },
    });
  }

  private resizeCanvas(): void {
    const canvas = this.canvas().nativeElement;
    const width = Math.max(1, canvas.clientWidth);
    const height = Math.max(1, canvas.clientHeight);
    const pixelRatio = window.devicePixelRatio || 1;
    canvas.width = Math.floor(width * pixelRatio);
    canvas.height = Math.floor(height * pixelRatio);
    this.scheduleDraw();
  }

  private fitMap(): void {
    const map = this.map();

    if (!map || !this.ready) {
      return;
    }

    const canvas = this.canvas().nativeElement;
    const availableWidth = Math.max(1, canvas.clientWidth - 36);
    const availableHeight = Math.max(1, canvas.clientHeight - 36);
    const scale = Math.min(
      2.5,
      Math.max(
        0.25,
        Math.min(
          availableWidth / (map.width * TILE_SIZE),
          availableHeight / (map.height * TILE_SIZE),
        ),
      ),
    );
    this.transform.set({
      tileSize: TILE_SIZE,
      scale,
      offsetX: (canvas.clientWidth - map.width * TILE_SIZE * scale) / 2,
      offsetY: (canvas.clientHeight - map.height * TILE_SIZE * scale) / 2,
    });
    this.scheduleDraw();
  }

  private scheduleDraw(): void {
    if (!this.ready || this.frameRequested) {
      return;
    }

    this.frameRequested = true;
    requestAnimationFrame(() => {
      this.frameRequested = false;
      this.draw();
    });
  }

  private draw(): void {
    const canvas = this.canvas().nativeElement;
    const context = canvas.getContext('2d');

    if (!context) {
      return;
    }

    const pixelRatio = window.devicePixelRatio || 1;
    const width = canvas.clientWidth;
    const height = canvas.clientHeight;
    context.setTransform(pixelRatio, 0, 0, pixelRatio, 0, 0);
    context.clearRect(0, 0, width, height);
    context.fillStyle = '#101718';
    context.fillRect(0, 0, width, height);

    const map = this.map();

    if (!map) {
      return;
    }

    const transform = this.transform();
    const renderedTileSize = transform.tileSize * transform.scale;

    for (const chunk of map.chunks) {
      for (const tile of chunk.tiles) {
        const screen = worldToScreen(tile, transform);

        if (
          screen.x + renderedTileSize < 0 ||
          screen.y + renderedTileSize < 0 ||
          screen.x > width ||
          screen.y > height
        ) {
          continue;
        }

        context.fillStyle = BIOME_COLORS[tile.biomeCode] ?? '#5e665f';
        context.fillRect(screen.x, screen.y, renderedTileSize + 0.5, renderedTileSize + 0.5);
        if (tile.hasResource) {
          context.fillStyle = tile.resourceExhausted ? '#787878' : '#4de0ad';
          context.beginPath();
          context.arc(
            screen.x + renderedTileSize * 0.25,
            screen.y + renderedTileSize * 0.25,
            Math.max(1.5, renderedTileSize * 0.12),
            0,
            Math.PI * 2,
          );
          context.fill();
        }
        if (tile.hasStructure) {
          context.fillStyle = '#f1c66d';
          const markerSize = Math.max(2, renderedTileSize * 0.38);
          context.fillRect(
            screen.x + (renderedTileSize - markerSize) / 2,
            screen.y + (renderedTileSize - markerSize) / 2,
            markerSize,
            markerSize,
          );
        }
      }

      const chunkOrigin = worldToScreen({ x: chunk.originX, y: chunk.originY }, transform);
      context.strokeStyle = 'rgba(238, 217, 169, 0.45)';
      context.lineWidth = Math.max(1, transform.scale);
      context.strokeRect(
        chunkOrigin.x,
        chunkOrigin.y,
        chunk.width * renderedTileSize,
        chunk.height * renderedTileSize,
      );
    }

    const selected = this.selectedTile();

    if (selected) {
      const screen = worldToScreen(selected, transform);
      context.strokeStyle = '#fff0ba';
      context.lineWidth = 2;
      context.strokeRect(
        screen.x + 1,
        screen.y + 1,
        Math.max(1, renderedTileSize - 2),
        Math.max(1, renderedTileSize - 2),
      );
    }

    for (const overlay of this.overlays()) {
      const screen = worldToScreen({ x: overlay.x + 0.5, y: overlay.y + 0.5 }, transform);
      context.beginPath();
      context.fillStyle = overlay.color ?? '#fff0ba';
      context.arc(screen.x, screen.y, Math.max(4, renderedTileSize * 0.32), 0, Math.PI * 2);
      context.fill();
    }
  }

  private updateHoveredPosition(event: MouseEvent): void {
    const map = this.map();

    if (!map) {
      return;
    }

    const position = screenToWorld(this.eventPoint(event), this.transform());
    this.hoveredPosition.set(
      position.x >= 0 && position.x < map.width && position.y >= 0 && position.y < map.height
        ? position
        : null,
    );
  }

  private eventPoint(event: MouseEvent): { x: number; y: number } {
    const rectangle = this.canvas().nativeElement.getBoundingClientRect();
    return { x: event.clientX - rectangle.left, y: event.clientY - rectangle.top };
  }

  private tileKey(x: number, y: number): string {
    return `${x}:${y}`;
  }
}
