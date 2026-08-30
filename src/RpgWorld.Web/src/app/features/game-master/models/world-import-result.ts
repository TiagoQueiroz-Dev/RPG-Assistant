export interface WorldImportResult {
  readonly worldId: string;
  readonly name: string;
  readonly width: number;
  readonly height: number;
  readonly chunkCount: number;
  readonly tileCount: number;
  readonly imageFormat: string;
  readonly status: 'completed';
}
