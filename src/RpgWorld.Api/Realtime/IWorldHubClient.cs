using RpgWorld.Application.Realtime;

namespace RpgWorld.Api.Realtime;

public interface IWorldHubClient
{
    Task WorldUpdated(WorldUpdateMessage message);
}

