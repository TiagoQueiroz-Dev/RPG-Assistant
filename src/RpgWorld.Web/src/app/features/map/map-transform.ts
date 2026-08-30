export interface ScreenPoint {
  readonly x: number;
  readonly y: number;
}

export interface WorldPoint {
  readonly x: number;
  readonly y: number;
}

export interface MapViewportTransform {
  readonly offsetX: number;
  readonly offsetY: number;
  readonly scale: number;
  readonly tileSize: number;
}

export function screenToWorld(
  point: ScreenPoint,
  transform: MapViewportTransform,
): WorldPoint {
  const renderedTileSize = transform.tileSize * transform.scale;

  return {
    x: Math.floor((point.x - transform.offsetX) / renderedTileSize),
    y: Math.floor((point.y - transform.offsetY) / renderedTileSize),
  };
}

export function worldToScreen(
  point: ScreenPoint,
  transform: MapViewportTransform,
): ScreenPoint {
  const renderedTileSize = transform.tileSize * transform.scale;

  return {
    x: point.x * renderedTileSize + transform.offsetX,
    y: point.y * renderedTileSize + transform.offsetY,
  };
}

export function zoomAt(
  transform: MapViewportTransform,
  anchor: ScreenPoint,
  factor: number,
  minimumScale = 0.25,
  maximumScale = 5,
): MapViewportTransform {
  const nextScale = Math.min(maximumScale, Math.max(minimumScale, transform.scale * factor));
  const worldX = (anchor.x - transform.offsetX) / (transform.tileSize * transform.scale);
  const worldY = (anchor.y - transform.offsetY) / (transform.tileSize * transform.scale);

  return {
    ...transform,
    scale: nextScale,
    offsetX: anchor.x - worldX * transform.tileSize * nextScale,
    offsetY: anchor.y - worldY * transform.tileSize * nextScale,
  };
}
