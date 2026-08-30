export interface WorldMapView {
  readonly worldId: string;
  readonly name: string;
  readonly width: number;
  readonly height: number;
  readonly chunkSize: number;
  readonly chunks: readonly WorldMapChunkView[];
}

export interface WorldMapChunkView {
  readonly x: number;
  readonly y: number;
  readonly originX: number;
  readonly originY: number;
  readonly width: number;
  readonly height: number;
  readonly tiles: readonly WorldMapTileView[];
}

export interface WorldMapTileView {
  readonly x: number;
  readonly y: number;
  readonly terrainCode: string;
  readonly biomeCode: string;
  readonly elevation: number;
}

export interface WorldMapOverlay {
  readonly id: string;
  readonly x: number;
  readonly y: number;
  readonly kind: 'entity' | 'structure' | 'marker';
  readonly label: string;
  readonly color?: string;
}
