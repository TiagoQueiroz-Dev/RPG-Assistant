import { computed, Injectable, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { environment } from '../../../environments/environment';

export interface WorldUpdateMessage {
  readonly messageId: string;
  readonly worldId: string;
  readonly updateType: string;
  readonly occurredAtUtc: string;
  readonly data: Readonly<Record<string, string | null>>;
}

type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

interface Subscription {
  readonly joinMethod: string;
  readonly leaveMethod: string;
  readonly id: string;
}

@Injectable({ providedIn: 'root' })
export class WorldRealtimeService {
  private readonly connection: HubConnection = new HubConnectionBuilder()
    .withUrl(environment.worldHubUrl)
    .withAutomaticReconnect([0, 2_000, 10_000, 30_000])
    .configureLogging(environment.production ? LogLevel.Warning : LogLevel.Information)
    .build();
  private readonly subscriptions = new Map<string, Subscription>();
  private readonly statusState = signal<ConnectionStatus>('disconnected');

  readonly status = this.statusState.asReadonly();
  readonly connected = computed(() => this.statusState() === 'connected');

  constructor() {
    this.connection.onreconnecting(() => this.statusState.set('reconnecting'));
    this.connection.onreconnected(async () => {
      this.statusState.set('connected');
      await this.restoreSubscriptions();
    });
    this.connection.onclose(() => this.statusState.set('disconnected'));
  }

  async connect(): Promise<void> {
    if (this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    this.statusState.set('connecting');
    try {
      await this.connection.start();
      this.statusState.set('connected');
    } catch (error) {
      this.statusState.set('disconnected');
      throw error;
    }
  }

  async disconnect(): Promise<void> {
    this.subscriptions.clear();
    await this.connection.stop();
  }

  onWorldUpdated(handler: (message: WorldUpdateMessage) => void): () => void {
    this.connection.on('WorldUpdated', handler);
    return () => this.connection.off('WorldUpdated', handler);
  }

  joinWorld(worldId: string): Promise<void> {
    return this.join('world', 'JoinWorld', 'LeaveWorld', worldId);
  }

  leaveWorld(worldId: string): Promise<void> {
    return this.leave('world', worldId);
  }

  joinChunk(chunkId: string): Promise<void> {
    return this.join('chunk', 'JoinChunk', 'LeaveChunk', chunkId);
  }

  leaveChunk(chunkId: string): Promise<void> {
    return this.leave('chunk', chunkId);
  }

  joinPlayer(playerId: string): Promise<void> {
    return this.join('player', 'JoinPlayer', 'LeavePlayer', playerId);
  }

  leavePlayer(playerId: string): Promise<void> {
    return this.leave('player', playerId);
  }

  joinGameMaster(worldId: string): Promise<void> {
    return this.join('gm', 'JoinGameMaster', 'LeaveGameMaster', worldId);
  }

  leaveGameMaster(worldId: string): Promise<void> {
    return this.leave('gm', worldId);
  }

  private async join(
    audience: string,
    joinMethod: string,
    leaveMethod: string,
    id: string,
  ): Promise<void> {
    await this.connect();
    await this.connection.invoke(joinMethod, id);
    this.subscriptions.set(`${audience}:${id}`, { joinMethod, leaveMethod, id });
  }

  private async leave(audience: string, id: string): Promise<void> {
    const key = `${audience}:${id}`;
    const subscription = this.subscriptions.get(key);
    if (!subscription) {
      return;
    }

    if (this.connection.state === HubConnectionState.Connected) {
      await this.connection.invoke(subscription.leaveMethod, id);
    }
    this.subscriptions.delete(key);
  }

  private async restoreSubscriptions(): Promise<void> {
    for (const subscription of this.subscriptions.values()) {
      await this.connection.invoke(subscription.joinMethod, subscription.id);
    }
  }
}
