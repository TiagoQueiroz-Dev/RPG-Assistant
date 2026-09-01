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
  readonly classificationOrigin: 'Automatic' | 'Manual';
  readonly classificationConfidence: number | null;
  readonly hasStructure: boolean;
  readonly hasResource: boolean;
  readonly resourceCode: string | null;
  readonly resourceQuantity: number | null;
  readonly resourceExhausted: boolean;
  readonly cityId: string | null;
  readonly cityName: string | null;
  readonly knowledgeState?: 'Discovered' | 'Known' | 'Visible' | null;
}

export interface WorldMapOverlay {
  readonly id: string;
  readonly x: number;
  readonly y: number;
  readonly kind: 'entity' | 'structure' | 'marker';
  readonly label: string;
  readonly color?: string;
}

export type WorldMapLayerMode =
  | 'Normal' | 'Political' | 'Population' | 'Economy' | 'Resources' | 'Military'
  | 'Religion' | 'Danger' | 'Biome' | 'Temperature' | 'Faction';

export interface WorldMapLayerView {
  readonly worldId: string;
  readonly mode: WorldMapLayerMode;
  readonly generatedAtUtc: string;
  readonly cells: readonly {
    readonly x: number;
    readonly y: number;
    readonly intensity: number;
    readonly label: string;
    readonly color: string;
    readonly entityId: string | null;
  }[];
  readonly legend: readonly {
    readonly label: string;
    readonly color: string;
    readonly minimum: number | null;
    readonly maximum: number | null;
  }[];
}
