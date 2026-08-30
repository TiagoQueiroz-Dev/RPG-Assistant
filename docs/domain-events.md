# Domain events

Domain aggregates derive from `AggregateRoot` and record immutable
`IDomainEvent` instances from domain operations. They never resolve or invoke
handlers directly.

Consumers implement `IDomainEventHandler<TEvent>`. The infrastructure dispatcher
resolves every registered handler for the concrete event type and invokes them
without coupling the producer to its consequences.

When aggregates are tracked by `RpgWorldDbContext`, events are dispatched only
after `SaveChanges` succeeds. A failed database operation leaves the events on
the aggregate and invokes no handler. Events are cleared only after all handlers
complete successfully.

Initial event contracts are available for actor death, resource discovery and
city creation. Feature issues will add their handlers and register them in the
composition root.
