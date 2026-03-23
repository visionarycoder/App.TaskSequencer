---
metadata:
  version: "1.0.0"
  created: "2025-01-09"
  section: "domain_driven_design"
  parent: "copilot_instructions.md"
  target_audience: "AI Code Assistant"

---

# Domain-Driven Design Patterns

## Repository Pattern

### Interface Design
```csharp
// ✅ GOOD: Segregated read/write repositories
public interface IReadRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id, CancellationToken ct);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct);
    IAsyncEnumerable<T> GetAllStreamAsync(CancellationToken ct);
}

public interface IWriteRepository<T> where T : class
{
    Task AddAsync(T entity, CancellationToken ct);
    Task UpdateAsync(T entity, CancellationToken ct);
    Task DeleteAsync(T entity, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

// ✅ GOOD: Domain-specific repository with business logic
public interface IComponentRepository : IReadRepository<Component>, IWriteRepository<Component>
{
    Task<IReadOnlyList<Component>> GetByStatusAsync(ComponentStatus status, CancellationToken ct);
    Task<bool> HasCircularDependenciesAsync(ComponentId id, CancellationToken ct);
}
```

### Implementation Pattern
```csharp
// ✅ GOOD: Implementation with DI
public class ComponentRepository : IComponentRepository
{
    private readonly DbContext _context;

    public ComponentRepository(DbContext context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<Component?> GetByIdAsync(object id, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(id);
        return await _context.Components.FindAsync([id], cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task AddAsync(Component entity, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await _context.Components.AddAsync(entity, ct).ConfigureAwait(false);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
```

---

## Specification Pattern

### Purpose
Encapsulate query logic, enabling reusable, testable, and composable filtering.

```csharp
// ✅ GOOD: Base specification
public abstract class Specification<T>
{
    public IQueryable<T> Apply(IQueryable<T> query)
    {
        query = ApplyFilter(query);
        query = ApplyIncludes(query);
        query = ApplySort(query);
        return query;
    }

    protected abstract IQueryable<T> ApplyFilter(IQueryable<T> query);
    protected abstract IQueryable<T> ApplyIncludes(IQueryable<T> query);
    protected abstract IQueryable<T> ApplySort(IQueryable<T> query);
}

// ✅ GOOD: Domain-specific specification
public class ActiveComponentsSpecification : Specification<Component>
{
    protected override IQueryable<Component> ApplyFilter(IQueryable<Component> query)
        => query.Where(c => c.Status == ComponentStatus.Active);

    protected override IQueryable<Component> ApplyIncludes(IQueryable<Component> query)
        => query.Include(c => c.Dependencies);

    protected override IQueryable<Component> ApplySort(IQueryable<Component> query)
        => query.OrderBy(c => c.Name);
}

// ✅ GOOD: Usage in repository
public async Task<IReadOnlyList<Component>> GetActiveComponentsAsync(CancellationToken ct)
{
    var spec = new ActiveComponentsSpecification();
    var query = spec.Apply(_context.Components);
    return await query.ToListAsync(ct).ConfigureAwait(false);
}
```

---

## Domain Events

### Publishing Pattern
```csharp
// ✅ GOOD: Domain event base
public abstract record class DomainEvent(DateTime OccurredAt = default)
{
    public DateTime OccurredAt { get; } = OccurredAt == default ? DateTime.UtcNow : OccurredAt;
    public Guid EventId { get; } = Guid.NewGuid();
}

// ✅ GOOD: Specific domain events
public record class ComponentCreatedEvent(ComponentId ComponentId, string Name) : DomainEvent;
public record class ComponentStatusChangedEvent(ComponentId ComponentId, ComponentStatus OldStatus, ComponentStatus NewStatus) : DomainEvent;

// ✅ GOOD: Aggregate root with domain events
public class Component
{
    private readonly List<DomainEvent> _events = [];
    public IReadOnlyList<DomainEvent> Events => _events.AsReadOnly();

    public void RaiseEvent(DomainEvent @event) => _events.Add(@event);

    public void ClearEvents() => _events.Clear();

    public void ChangeStatus(ComponentStatus newStatus)
    {
        if (Status == newStatus) return;

        var oldStatus = Status;
        Status = newStatus;
        RaiseEvent(new ComponentStatusChangedEvent(Id, oldStatus, newStatus));
    }
}
```

### Event Publishing
```csharp
// ✅ GOOD: Publisher interface
public interface IDomainEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct) where TEvent : DomainEvent;
}

// ✅ GOOD: Usage in repository
public async Task SaveChangesAsync(CancellationToken ct)
{
    await _context.SaveChangesAsync(ct).ConfigureAwait(false);

    var entities = _context.ChangeTracker.Entries<IAggregateRoot>()
        .Select(e => e.Entity)
        .ToList();

    foreach (var entity in entities)
    {
        foreach (var @event in entity.Events)
        {
            await _eventPublisher.PublishAsync(@event, ct).ConfigureAwait(false);
        }
        entity.ClearEvents();
    }
}
```

---

## Entity vs Value Object Invariants

### Validation at Boundary
```csharp
// ✅ GOOD: Constructor validates invariants
public class Component
{
    public ComponentId Id { get; }
    public string Name { get; private set; }
    public IReadOnlySet<ComponentId> Dependencies { get; private set; }

    public Component(ComponentId id, string name, IReadOnlySet<ComponentId> dependencies)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(dependencies);

        if (name.Length > 255)
            throw new ArgumentException("Name cannot exceed 255 characters", nameof(name));

        Id = id;
        Name = name;
        Dependencies = dependencies;
    }

    public void UpdateName(string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        if (newName.Length > 255)
            throw new ArgumentException("Name cannot exceed 255 characters", nameof(newName));

        Name = newName;
    }
}
```

---

## Aggregate Root Pattern

```csharp
// ✅ GOOD: Interface for aggregate roots
public interface IAggregateRoot
{
    IReadOnlyList<DomainEvent> Events { get; }
    void ClearEvents();
}

// ✅ GOOD: Implementation
public class ComponentAggregate : IAggregateRoot
{
    private readonly List<DomainEvent> _events = [];

    public ComponentId Id { get; }
    public string Name { get; private set; }
    public ComponentStatus Status { get; private set; }
    public IReadOnlySet<ComponentId> Dependencies { get; private set; }

    public IReadOnlyList<DomainEvent> Events => _events.AsReadOnly();

    public void ClearEvents() => _events.Clear();

    private void RaiseEvent(DomainEvent @event) => _events.Add(@event);

    public void ChangeStatus(ComponentStatus newStatus)
    {
        if (Status == newStatus) return;
        Status = newStatus;
        RaiseEvent(new ComponentStatusChangedEvent(Id, Status, newStatus));
    }
}
```

---

## Anti-Patterns

| Pattern | Issue |
|---------|-------|
| Repository returns IQueryable | Leaks persistence concerns to caller |
| Anemic domain models | Business logic scattered across services |
| Multiple aggregates in transaction | Violates consistency boundary |
| Repositories for everything | Value objects don't need repositories |
| Domain events not published | Business logic isolation lost |
