namespace RpgWorld.Application.Worlds.Editing;

public sealed record MapPaintRequest(
    MapBrushKind Brush,
    int CenterX,
    int CenterY,
    int Size);
