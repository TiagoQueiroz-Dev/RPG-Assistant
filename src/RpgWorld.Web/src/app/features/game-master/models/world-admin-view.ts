export interface WorldAdminView {
  readonly worldId: string;
  readonly name: string;
  readonly currentTime: string;
  readonly simulationStatus: 'running' | 'paused' | 'maintenance';
  readonly activeChunks: number;
  readonly totalActors: number;
  readonly cities: readonly AdminCitySummary[];
  readonly factions: readonly AdminFactionSummary[];
}

export interface AdminCitySummary {
  readonly cityId: string;
  readonly name: string;
  readonly population: number;
  readonly foodStockDays: number;
  readonly status?: 'Active' | 'Crisis' | 'Destroyed';
  readonly wealth?: number;
}

export interface AdminFactionSummary {
  readonly factionId: string;
  readonly name: string;
  readonly power: number;
  readonly disposition: 'allied' | 'neutral' | 'hostile';
}
