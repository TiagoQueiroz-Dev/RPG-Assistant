export interface ActorAtPositionView {
  readonly actorId: string;
  readonly name: string;
  readonly kind: string;
  readonly currentAction: string | null;
  readonly traitCodes: readonly string[];
}

export interface NpcTraitInspectorView {
  readonly code: string;
  readonly name: string;
  readonly description: string;
  readonly actionScoreMultipliers: Readonly<Record<string, number>>;
  readonly definitionAvailable: boolean;
}

export interface NpcInspectorView {
  readonly actorId: string;
  readonly worldId: string;
  readonly name: string;
  readonly x: number;
  readonly y: number;
  readonly health: number;
  readonly maximumHealth: number;
  readonly hunger: number;
  readonly energy: number;
  readonly money: number;
  readonly job: string | null;
  readonly currentAction: string | null;
  readonly factionId: string | null;
  readonly traits: readonly NpcTraitInspectorView[];
  readonly memories: readonly NpcMemoryInspectorView[];
}

export interface NpcMemoryInspectorView {
  readonly memoryId: string;
  readonly eventType: string;
  readonly targetId: string | null;
  readonly importance: number;
  readonly createdAt: string;
  readonly expiresAt: string | null;
  readonly payload: Readonly<Record<string, string>>;
}
