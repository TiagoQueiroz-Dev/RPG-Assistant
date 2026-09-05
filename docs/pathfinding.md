# Actor pathfinding

`IActorPathfinder.FindAsync` accepts an actor, destination, optional processing limits, and cancellation token.
The A* implementation returns ordered adjacent steps excluding the origin and including the destination, total movement
cost, expanded node count, and Found/NoPath/SearchLimitReached status. An actor already at the destination has an empty
successful route. The pathfinder does not move the actor or persist state.

Navigation uses the same terrain/biome costs and eight-way adjacency policy as actor movement, including its diagonal
rules. With the default policy, the heuristic is Chebyshev distance times the cheapest rounded step in the search area.
Custom movement policies use a zero heuristic to preserve optimality for arbitrary nonnegative costs. Queue ties are
resolved deterministically by remaining distance and tile coordinates. Each request loads fresh map state and effective
campaign definitions, so changes to terrain are respected when recomputing a route.

Defaults limit expansion to 10000 nodes and map loading to 65536 tiles. The search rectangle encloses origin and destination
with 16 tiles of padding, clipped to the world. Hitting either budget returns SearchLimitReached without a partial route.
Exhausting a cropped search area also returns SearchLimitReached: callers may widen it before concluding that no route
exists. A blocked destination or exhausted full-world search returns NoPath. Invalid endpoints/options throw explicit
argument errors; cancellation propagates. Tile reads are bounded and use the persisted map without entity tracking.
