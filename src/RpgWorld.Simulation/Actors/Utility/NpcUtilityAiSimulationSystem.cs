using Microsoft.Extensions.Logging;
using RpgWorld.Application.Actors;
using RpgWorld.Application.Actors.Memories;
using RpgWorld.Domain.Actors.Memories;
using RpgWorld.Simulation.Engine;

namespace RpgWorld.Simulation.Actors.Utility;

public sealed class NpcUtilityAiSimulationSystem : ISimulationSystem
{
    private readonly INpcNeedsRepository _repository;
    private readonly INpcDecisionContextProvider _contextProvider;
    private readonly INpcUtilityDecisionService _decisionService;
    private readonly INpcDecisionDiagnostics _diagnostics;
    private readonly ILogger<NpcUtilityAiSimulationSystem> _logger;
    private readonly INpcMemoryRepository _memories;
    private readonly NpcMemoryOptions _memoryOptions;

    public NpcUtilityAiSimulationSystem(
        INpcNeedsRepository repository,
        INpcDecisionContextProvider contextProvider,
        INpcUtilityDecisionService decisionService,
        INpcDecisionDiagnostics diagnostics,
        ILogger<NpcUtilityAiSimulationSystem> logger)
        : this(repository, contextProvider, decisionService, diagnostics, logger,
            new EmptyNpcMemoryRepository(), new NpcMemoryOptions()) { }

    public NpcUtilityAiSimulationSystem(
        INpcNeedsRepository repository,
        INpcDecisionContextProvider contextProvider,
        INpcUtilityDecisionService decisionService,
        INpcDecisionDiagnostics diagnostics,
        ILogger<NpcUtilityAiSimulationSystem> logger,
        INpcMemoryRepository memories,
        NpcMemoryOptions memoryOptions)
    {
        _repository = repository;
        _contextProvider = contextProvider;
        _decisionService = decisionService;
        _diagnostics = diagnostics;
        _logger = logger;
        _memories = memories;
        _memoryOptions = memoryOptions;
    }

    public string Name => "NpcUtilityAi";
    public int Order => 30;
    public TimeSpan Frequency => SimulationSystemFrequencies.NpcDecisions;

    public async Task ExecuteAsync(
        SimulationTickContext context,
        CancellationToken cancellationToken = default)
    {
        var npcs = await _repository.ListForUpdateAsync(context.WorldId, cancellationToken);
        context.RecordActorsProcessed(npcs.Count);
        var relevantMemories = await _memories.ListRelevantForActorsAsync(
            npcs.Select(npc => npc.Id).ToArray(),
            context.Clock.CurrentInstant,
            _memoryOptions.MinimumDecisionImportance,
            cancellationToken);
        var memoriesByActor = relevantMemories
            .GroupBy(memory => memory.ActorId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<NpcMemory>)group.ToArray());
        var changed = false;
        foreach (var npc in npcs)
        {
            // A repeated world tick must not restart an action already executed at this instant.
            if (npc.ActionExecution?.LastProcessedAt is { } processed && processed >= context.Clock.CurrentInstant)
                continue;
            var memories = memoriesByActor.GetValueOrDefault(npc.Id) ?? [];
            var decision = _decisionService.Decide(await _contextProvider.CreateAsync(npc, memories, cancellationToken));
            var explanation = decision?.Explain() ?? "No eligible NPC action was available.";
            _diagnostics.Record(new NpcDecisionDiagnostic(
                context.WorldId,
                npc.Id,
                context.Clock.CurrentInstant,
                decision,
                explanation));
            _logger.LogDebug(
                "NPC utility decision for {ActorId} in world {WorldId}: {Explanation}",
                npc.Id,
                context.WorldId,
                explanation);
            var actionCode = decision?.ActionCode;
            changed |= npc.SelectAction(actionCode, context.Clock.CurrentInstant);
        }
        if (changed) await _repository.SaveChangesAsync(cancellationToken);
    }

    private sealed class EmptyNpcMemoryRepository : INpcMemoryRepository
    {
        public void Add(NpcMemory memory) => throw new NotSupportedException();
        public Task<IReadOnlyList<NpcMemory>> ListAsync(Guid actorId, Guid? targetId, DateTimeOffset asOf, int minimumImportance = 1, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NpcMemory>>([]);
        public Task<IReadOnlyList<NpcMemory>> ListRelevantForActorsAsync(IReadOnlyCollection<Guid> actorIds, DateTimeOffset asOf, int minimumImportance, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NpcMemory>>([]);
        public Task<int> DeleteExpiredAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
