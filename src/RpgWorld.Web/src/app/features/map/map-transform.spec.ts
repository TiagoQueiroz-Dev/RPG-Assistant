import { screenToWorld, worldToScreen, zoomAt } from './map-transform';

describe('map coordinate transforms', () => {
  const transform = { offsetX: 40, offsetY: 20, scale: 2, tileSize: 10 };

  it('converts screen positions to exact world tiles at their borders', () => {
    expect(screenToWorld({ x: 40, y: 20 }, transform)).toEqual({ x: 0, y: 0 });
    expect(screenToWorld({ x: 99, y: 79 }, transform)).toEqual({ x: 2, y: 2 });
    expect(screenToWorld({ x: 100, y: 80 }, transform)).toEqual({ x: 3, y: 3 });
  });

  it('keeps the world position under the pointer fixed while zooming', () => {
    const anchor = worldToScreen({ x: 4.5, y: 3.25 }, transform);
    const zoomed = zoomAt(transform, anchor, 1.5);

    expect(worldToScreen({ x: 4.5, y: 3.25 }, zoomed)).toEqual(anchor);
  });

  it('clamps zoom to supported limits', () => {
    expect(zoomAt(transform, { x: 0, y: 0 }, 100).scale).toBe(5);
    expect(zoomAt(transform, { x: 0, y: 0 }, 0.001).scale).toBe(0.25);
  });
});
