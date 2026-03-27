# Code Quality, Architecture & Design

Design principles, code metrics, and architectural patterns for development.

---

## Table of Contents

1. [Code Quality Metrics](#code-quality-metrics)
2. [Primitive Obsession Avoidance (CRITICAL)](#primitive-obsession-avoidance-critical)
3. [DDD Project Structure](#ddd-project-structure)
4. [Value Objects & Strong Typing](#value-objects--strong-typing)
5. [SOLID Principles](#solid-principles)
6. [Dependency Injection Pattern](#dependency-injection-pattern)
7. [Error Handling](#error-handling)
8. [Comments & Documentation](#comments--documentation)

---

## Code Quality Metrics

### Hard Requirements (Non-Negotiable)

These are measured and enforced:

| Metric | Limit | Rationale |
|--------|-------|-----------|
| **Method Length** | 30 lines max (excluding comments) | Forces single responsibility |
| **Cyclomatic Complexity** | 10 max per method | Reduces cognitive load |
| **Constructor Parameters** | 5 max | Signals design issues if exceeded |
| **Class Responsibilities** | 1 (Single Responsibility) | SOLID principle |

### Quality Checkpoints

```csharp
// ❌ TOO LONG: 35 lines (exceeds 30-line limit)
public async Task ProcessAsync(IEnumerable<ComponentId> ids, CancellationToken ct)
{
    var components = await _repository.GetByIdsAsync(ids, ct);
    foreach (var component in components)
    {
        ValidateComponent(component);
        var dependencies = component.GetDependencies();
        foreach (var dep in dependencies)
        {
            var depComponent = await _repository.GetByIdAsync(dep, ct);
            if (depComponent == null) throw new InvalidOperationException();
            // ... 25 more lines of logic
        }
    }
}

// ✅ REFACTORED: Split into smaller methods
public async Task ProcessAsync(IEnumerable<ComponentId> ids, CancellationToken ct)
{
    var components = await _repository.GetByIdsAsync(ids, ct);
    foreach (var component in components)
    {
        await ProcessComponentAsync(component, ct);
    }
}

private async Task ProcessComponentAsync(Component component, CancellationToken ct)
{
    ValidateComponent(component);
    var dependencies = component.GetDependencies();
    await ProcessDependenciesAsync(dependencies, ct);
}
```

---

## Primitive Obsession Avoidance (CRITICAL)

### Definition & Impact

**Primitive Obsession** = Using primitive types (string, int, guid, etc.) directly to represent domain concepts instead of creating appropriate value objects.

**Impact**: 
- ❌ Type system doesn't enforce meaning
- ❌ Same primitive used for different concepts (confusing)
- ❌ Validation logic scattered across codebase
- ❌ IDE can't help prevent mixing up similar IDs
- ❌ Tests are harder to understand

**Benefit of Value Objects**:
- ✅ Compiler enforces correct types
- ✅ Self-documenting code
- ✅ Single place to validate
- ✅ IDE autocomplete shows intent
- ✅ Easier to test and refactor

### Mandatory Rule

**Every domain concept must be represented by a dedicated type. Do not pass raw primitives between layers.**

### Primitive Types Requiring Value Objects

| Primitive | Domain Concept | Value Object Example | When Primitive OK |
|-----------|-----------------|----------------------|-------------------|
| `Guid` | Identity/ID | `ComponentId : Identifier<Guid>` | Local variable (single method) |
| `string` | Email, Phone, Code | `EmailAddress`, `PhoneNumber` | Local variable only |
| `int` | Quantity, Max, Duration | `Quantity`, `MaxRetries` | Local variable only |
| `decimal` | Money, Percentage | `Money`, `Percentage` | Local variable only |
| `DateTime` | Business dates | `ScheduledDate`, `CompletionTime` | Local variable only |
| `bool` | Business states | `IsActive : Identifier<bool>` | Local variable only |
| `IEnumerable<string>` | Typed collections | `ComponentIds : IReadOnlySet<ComponentId>` | Never |

### Anti-Patterns (All CRITICAL Violations)

```csharp
// ❌ CRITICAL: Raw string for email
public async Task SendNotificationAsync(string email, CancellationToken ct)
{
    var message = $"Task completed for {email}";
    await _emailService.SendAsync(email, message, ct);
}

// ❌ CRITICAL: Raw Guid for identity
public class Component
{
    public Guid ComponentId { get; set; }  // What if confused with TaskId?
    public Guid TaskId { get; set; }
}

// ❌ CRITICAL: Raw int for business concept
public async Task RetryAsync(int maxRetries, CancellationToken ct)
{
    if (maxRetries < 0) throw new ArgumentException(); // Validation scattered
}

// ❌ CRITICAL: Raw collection of strings for typed IDs
public class Component
{
    public IReadOnlyList<string> DependencyIds { get; set; }  // Any string? ComponentIds?
}

// ❌ CRITICAL: Multiple primitives representing one concept
public void ScheduleTask(string taskId, DateTime start, DateTime end, bool isActive)
{
    // What is "isActive"? Is it part of task definition or scheduling?
    var task = new Task { Id = taskId, StartTime = start, EndTime = end };
}
```

### Correct Implementations

#### Generic Identifier Base Class

```csharp
/// <summary>
/// Base class for all typed identifiers. Prevents mixing different ID types.
/// </summary>
public abstract class Identifier<T>(T value) where T : notnull
{
    public T Value { get; } = value;
    
    public override bool Equals(object? obj) =>
        obj is Identifier<T> other && EqualityComparer<T>.Default.Equals(Value, other.Value);
    
    public override int GetHashCode() => Value.GetHashCode();
    
    public override string ToString() => Value.ToString()!;
}
```

#### Typed Identifiers

```csharp
// ✅ GOOD: Specific ID type prevents accidental mixing
public class ComponentId : Identifier<Guid>
{
    public ComponentId(Guid value) : base(value) { }
}

public class TaskId : Identifier<Guid>
{
    public TaskId(Guid value) : base(value) { }
}

// ✅ GOOD: Now compiler prevents mistakes
public class Component(ComponentId id, IEnumerable<TaskId> taskIds)
{
    public ComponentId Id { get; } = id;
    public IReadOnlySet<TaskId> TaskIds { get; } = taskIds.ToHashSet();
}

// ❌ WON'T COMPILE: Compiler catches the mistake
var task = new Component(new TaskId(guid), []); // Error: TaskId is not ComponentId
```

#### Strong Value Objects

```csharp
// ✅ GOOD: String value object with validation
public record EmailAddress
{
    public string Value { get; init; }
    
    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be empty");
        
        if (!value.Contains("@"))
            throw new ArgumentException("Invalid email format");
        
        Value = value;
    }
    
    public override string ToString() => Value;
}

// ✅ GOOD: Usage is now type-safe
public async Task SendAsync(EmailAddress to, string message, CancellationToken ct)
{
    // Type system enforces that 'to' is a valid email
    await _emailService.SendAsync(to.Value, message, ct);
}
```

#### Numeric Value Objects

```csharp
// ✅ GOOD: Encapsulate business rules once
public record Quantity
{
    public int Value { get; init; }
    
    public Quantity(int value)
    {
        if (value <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(value));
        
        Value = value;
    }
    
    public static Quantity operator *(Quantity left, decimal percent) =>
        new((int)(left.Value * percent / 100));
}

// ✅ GOOD: Business rule validated in one place
public async Task OrderAsync(Quantity quantity, CancellationToken ct)
{
    // Quantity.Value is guaranteed > 0
    await _orderService.CreateAsync(quantity.Value, ct);
}
```

#### Collections of Value Objects

```csharp
// ✅ GOOD: Typed collection of IDs
public record ComponentDependencies(IReadOnlySet<ComponentId> Dependencies)
{
    // Business rule: enforced at creation
    public ComponentDependencies(IEnumerable<ComponentId> deps) 
        : this(new HashSet<ComponentId>(deps ?? [])) { }
    
    public static ComponentDependencies None => new([]);
}

// ✅ GOOD: No primitive collection confusion
public class Component(ComponentId id, ComponentDependencies dependencies)
{
    public ComponentId Id { get; } = id;
    public ComponentDependencies Dependencies { get; } = dependencies;
}
```

### Refactoring Example: Before & After

#### BEFORE: Primitive Obsession

```csharp
public class ComponentService
{
    public async Task ExecuteAsync(
        string componentId,
        IEnumerable<string> dependencyIds,
        string componentName,
        int maxRetries,
        string email)
    {
        foreach (var depId in dependencyIds)
        {
            await ProcessDependencyAsync(depId, email);  // What if depId is an email?
        }
        
        if (maxRetries < 0)  // Validation scattered; validation also in RetryPolicy
            throw new InvalidOperationException();
    }
}
```

#### AFTER: Strong Typing

```csharp
public class ComponentService(
    IComponentRepository repository,
    IEmailNotifier emailer)
{
    public async Task ExecuteAsync(
        ComponentId componentId,
        ComponentDependencies dependencies,
        ComponentName name,
        MaxRetries maxRetries,
        EmailAddress email,
        CancellationToken ct)
    {
        // Type system enforces meaning. No mixing up dependencies with email
        foreach (var depId in dependencies.Dependencies)
        {
            await ProcessDependencyAsync(depId, email, ct);
        }
        
        // Validation happened at construction time for all value objects
        await emailer.NotifyAsync(email, $"Component {name} executed", ct);
    }
}
```

**Advantages**:
- `ComponentId` vs `TaskId` can't be confused
- `maxRetries` validated once at construction
- `email` verified as valid email address
- IDE autocomplete shows intent clearly
- Compiler prevents bugs before runtime

---

## DDD Project Structure

### Recommended Organization

```
src/
├── Domain/
│   ├── Aggregates/           # Root entities (e.g., Component.cs, Task.cs)
│   ├── ValueObjects/         # Immutable values (e.g., ComponentId.cs)
│   ├── Entities/             # Domain entities (non-roots)
│   ├── DomainEvents/         # Events raised by domain logic
│   ├── Services/             # Domain services (cross-aggregate logic)
│   └── Exceptions/           # Domain-specific exceptions
├── Application/
│   ├── Services/             # Use cases / workflows
│   ├── Handlers/             # Command/event handlers (CQRS)
│   ├── DTOs/                 # Data transfer objects
│   └── Validators/           # Input validation services
├── Infrastructure/
│   ├── Persistence/          # Repository implementations
│   ├── ExternalServices/     # Third-party integrations
│   ├── Configuration/        # DI setup
│   └── Migrations/           # Database migrations
└── Presentation/
    ├── Controllers/          # REST endpoints (or Grains for Orleans)
    ├── Hubs/                 # SignalR hubs (if real-time)
    └── Models/               # API request/response models
```

---

## Value Objects & Strong Typing

### Value Object Pattern Template

```csharp
// Create this template for all new value objects
public record ValueObjectName(string Value)
{
    // Validation in constructor
    public ValueObjectName(string value) : this(ValidateAndNormalize(value)) { }
    
    // Implicit conversion from primitive (optional convenience)
    public static implicit operator ValueObjectName(string value) => new(value);
    public static implicit operator string(ValueObjectName obj) => obj.Value;
    
    private static string ValidateAndNormalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty", nameof(value));
        
        return value.Trim();
    }
    
    public override string ToString() => Value;
}
```

---

## SOLID Principles

### Principle → Enforcement Table

| Principle | Meaning | Enforcement | Violation Example |
|-----------|---------|--------------|-------------------|
| **SRP** | Single Responsibility | One reason to change | Mixing persistence + business logic |
| **OCP** | Open/Closed | Extend, don't modify | Adding if/else for new behaviors |
| **LSP** | Liskov Substitution | Substitutable derived types | Throwing NotImplementedException override |
| **ISP** | Interface Segregation | Segregated, focused interfaces | `IRepository<T>` should split to `IReadRepository<T>` + `IWriteRepository<T>` |
| **DIP** | Dependency Inversion | Depend on abstractions | Direct database calls (use repository interface) |

### SRP Example

```csharp
// ❌ VIOLATES SRP: Two reasons to change
public class UserService
{
    public void RegisterUser(User user)  // Registration logic
    {
        ValidateUser(user);
        _db.Users.Add(user);  // Persistence logic mixed in
    }
}

// ✅ FOLLOWS SRP: Single responsibility
public class UserService
{
    private readonly IUserRepository _repository;
    
    public void RegisterUser(User user)
    {
        ValidateUser(user);
        _repository.AddAsync(user);
    }
}
```

---

## Dependency Injection Pattern

### Correct DI Pattern

```csharp
// ✅ GOOD: Constructor injection
public class ComponentService
{
    private readonly IComponentRepository _repository;
    private readonly ILogger<ComponentService> _logger;
    
    public ComponentService(IComponentRepository repository, ILogger<ComponentService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    public async Task ExecuteAsync(ComponentId id, CancellationToken ct)
    {
        var component = await _repository.GetAsync(id, ct);
        _logger.LogInformation("Processing {ComponentId}", id);
    }
}

// Setup in Dependency Container
services.AddScoped<IComponentRepository, ComponentRepository>();
services.AddScoped<ComponentService>();
```

### Anti-Patterns

```csharp
// ❌ AVOID: Service locator pattern
var service = ServiceLocator.GetService<IComponentRepository>();

// ❌ AVOID: Static methods/classes
public static class ComponentHelper { public static void Execute() { } }

// ❌ AVOID: Hard-coded dependencies
public class ComponentService
{
    private readonly IComponentRepository _repo = new ComponentRepository();  // Tightly coupled!
}
```

---

## Error Handling

### Typed Exceptions with Context

```csharp
// ✅ GOOD: Specific exception with context
throw new InvalidOperationException(
    $"Component '{componentId.Value}' cannot transition from " +
    $"{currentStatus} to {newStatus}. " +
    $"Valid transitions: {string.Join(", ", validTransitions)}");

// ❌ AVOID: Generic messages
throw new Exception("Error");

// ❌ AVOID: No context
throw new InvalidOperationException("Invalid state");
```

### Exception Handling Strategy

```csharp
// ✅ GOOD: Catch specific exceptions with recovery strategy
try
{
    await _externalService.CallAsync(ct);
}
catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
{
    _logger.LogWarning("Service temporarily unavailable, will retry");
    await Task.Delay(TimeSpan.FromSeconds(5), ct);
    await _externalService.CallAsync(ct);
}
catch (OperationCanceledException)
{
    _logger.LogInformation("Operation was cancelled");
    throw;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error calling external service");
    throw;
}
```

---

## Comments & Documentation

### Comment Guidelines

| Type | When to Use | Example |
|------|------------|---------|
| **Why Comments** | Explain non-obvious logic | `// Retry 3 times due to known API throttling (see TICKET-123)` |
| **What Comments** | Forbidden | ❌ `// increment counter` |
| **XML Docs** | Public API surface only | `/// <summary>Processes component dependencies</summary>` |

### Documentation Standards

```csharp
/// <summary>
/// Executes the component and its dependencies in order.
/// </summary>
/// <param name="id">The component to execute</param>
/// <param name="ct">Cancellation token</param>
/// <returns>The execution result</returns>
public async Task<ExecutionResult> ExecuteAsync(ComponentId id, CancellationToken ct)
{
    var component = await _repository.GetAsync(id, ct);
    
    // Why: Dependencies must execute before parent to maintain causality
    var dependencies = await ResolveDependenciesAsync(component, ct);
    
    var result = ExecuteInOrder(dependencies, component);
    return result;
}
```

---

## Summary Checklist

### Before Submitting Code

- [ ] All methods ≤ 30 lines
- [ ] Cyclomatic complexity ≤ 10 per method
- [ ] No primitive obsession (all domain concepts are typed)
- [ ] Async methods end with `Async` suffix
- [ ] All async methods have `CancellationToken ct` parameter
- [ ] No underscore field prefixes
- [ ] DI for all dependencies
- [ ] Specific exceptions with context
- [ ] Comments explain "why", not "what"
- [ ] SOLID principles followed

---

## See Also

- [Coding Standards](01-coding-standards.md) - How to format code
- [Async Requirements](02-async-requirements.md) - Async/cancellation patterns
- [DDD Patterns](../patterns/01-ddd.md) - Repository, Specification patterns
