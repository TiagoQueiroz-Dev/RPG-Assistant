import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';

export interface CityMasterView {
  readonly cityId: string;
  readonly worldId: string;
  readonly name: string;
  readonly status: 'Active' | 'Crisis' | 'Destroyed';
  readonly population: number;
  readonly wealth: number;
  readonly resourceStocks: Readonly<Record<string, number>>;
  readonly resourceMarkets: Readonly<Record<string, CityResourceMarketView>>;
  readonly economicCycleCount: number;
  readonly lastEconomicCycleAtUtc: string | null;
}

export interface CityResourceMarketView {
  readonly resourceCode: string;
  readonly openingStock: number;
  readonly produced: number;
  readonly demand: number;
  readonly consumed: number;
  readonly unmetDemand: number;
  readonly closingStock: number;
  readonly unitPrice: number;
  readonly condition: 'Balanced' | 'Shortage' | 'Surplus';
  readonly updatedAtUtc: string;
}

@Injectable({ providedIn: 'root' })
export class CityService {
  private readonly api = inject(ApiClient);

  list(worldId: string): Observable<readonly CityMasterView[]> {
    return this.api.get<readonly CityMasterView[]>(`worlds/${worldId}/cities`);
  }
}
