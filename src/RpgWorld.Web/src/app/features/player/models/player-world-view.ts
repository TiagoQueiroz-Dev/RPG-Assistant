export interface PlayerWorldView {
  readonly playerActorId: string;
  readonly worldId: string;
  readonly worldName: string;
  readonly characterName: string;
  readonly localTime: string;
  readonly currentLocation: PlayerLocationView;
  readonly nearbyEntities: readonly VisibleEntityView[];
  readonly discoveredRegions: readonly DiscoveredRegionView[];
}

export interface PlayerLocationView {
  readonly name: string;
  readonly description: string;
  readonly weather: string;
}

export interface VisibleEntityView {
  readonly entityId: string;
  readonly displayName: string;
  readonly kind: 'person' | 'creature' | 'landmark';
  readonly category?: 'npc' | 'player' | 'creature' | 'merchant' | 'guard';
  readonly distance: string;
}

export interface DiscoveredRegionView {
  readonly regionId: string;
  readonly name: string;
  readonly knowledge: 'discovered' | 'known' | 'visible';
}
