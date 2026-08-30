namespace RpgWorld.Application.Worlds.Importing;

public interface IMapRegionClassifier
{
    MapRegionClassification Classify(MapColorSample sample);
}
