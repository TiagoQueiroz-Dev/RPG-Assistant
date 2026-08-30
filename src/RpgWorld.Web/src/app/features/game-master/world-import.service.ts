import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '../../core/api/api-client.service';
import { WorldImportResult } from './models/world-import-result';

@Injectable({ providedIn: 'root' })
export class WorldImportService {
  private readonly api = inject(ApiClient);

  import(file: File, name: string, gridResolution: number): Observable<WorldImportResult> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('name', name);
    form.append('gridResolution', gridResolution.toString());
    return this.api.post<WorldImportResult, FormData>('worlds/import', form);
  }
}
