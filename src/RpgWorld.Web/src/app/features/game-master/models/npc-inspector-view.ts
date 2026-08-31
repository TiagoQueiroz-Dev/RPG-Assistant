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
}
