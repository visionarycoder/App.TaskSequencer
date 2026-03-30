---
metadata:
  version: "1.3.1"
  created: "2025-01-09"
  modified: "2025-01-09"
  project: "Dependencies"
  target_audience: "AI Code Assistant"
  priority: "critical"
  applies_to:
    - "all_code"
    - "all_contributions"
  sections: 31
  location: "docs/"
  critical_rules:
    - "Async method naming & CancellationToken requirements"
    - "Primitive obsession avoidance"
    - "No underscore field prefixes"
    - "Immutability by default"
    - "Sealed classes unless intentionally designed for inheritance"
    - "Strong typing (no raw primitives)"
  split_out_files:
    - "docs/patterns_ddd.md: Repository, Specification, Domain Events"
    - "docs/patterns_validation.md: Validation, Error Handling, Result Pattern"
    - "docs/patterns_testing.md: Unit & Integration Testing"

---

# Copilot Instructions for Dependencies Project

## 1. TECHNOLOGY DEFINITIONS

### Runtime & Language Configuration
- **Target Framework**: .NET 10
- **C# Version**: 14+ (latest features required)
- **Null Safety**: `<Nullable>enable</Nullable>` mandatory in all projects
- **Async Pattern**: async/await throughout; `Task.Result` and blocking calls forbidden
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection exclusively

---

## 2. CODING STANDARDS

### 2.1 Naming Conventions
| Context | Pattern | Valid | Invalid | Example |
|---------|---------|-------|---------|---------|
| Public Properties | PascalCase | ✅ | ❌ | `public string Name { get; set; }` |
| Public Methods | PascalCase | ✅ | ❌ | `public void Execute()` |
| **Async Methods** | **PascalCase + Async suffix** | ✅ | ❌ | `public Task ExecuteAsync(CancellationToken ct)` |
| **CancellationToken Parameter** | **Always `ct`** | ✅ | ❌ | `CancellationToken ct` (not `cancellationToken`) |
| Public Classes/Records | PascalCase | ✅ | ❌ | `public class ComponentId` |
| Local Variables | camelCase | ✅ | ❌ | `var componentName = "..."` |
| Method Parameters | camelCase | ✅ | ❌ | `void Execute(string taskName)` |
| Constants | UPPER_SNAKE_CASE | ✅ | ❌ | `const int MAX_RETRIES = 3` |
| Private Fields | Property-backed (no prefix) | ✅ | ❌ | `private string name;` |
| Underscore Prefix | Forbidden | ❌ | ✅ | Never use `_name` |

### 2.2 C# 14+ Language Features (Priority Order)

#### 1. Primary Constructors (Highest Priority)
```csharp
// ✅ GOOD: Reduces boilerplate
public class Component(ComponentId componentId, string name, string description = "")
{
    public ComponentId ComponentId { get; } = componentId;
    public string Name { get; } = name;
}
```

#### 2. Init-only Properties
```csharp
// ✅ GOOD: Immutable value objects
public record struct ComponentData(Guid Id, string Name);
public class Task { public required string Name { get; init; } }
```

#### 3. Records (Value Objects & DTOs)
```csharp
// ✅ GOOD: Automatic equality, ToString, GetHashCode
public record ComponentMetadata(Guid Id, string Name, DateTime CreatedAt);
```

#### 4. File-Scoped Types
```csharp
// ✅ GOOD: Internal implementation details
file class ComponentValidator { }
```

#### 5. Collection Expressions
```csharp
// ✅ GOOD: Empty collections
var items = [];
var expanded = [..existingItems, newItem];
```

#### 6. Global Usings (GlobalUsings.cs)
```csharp
// ✅ GOOD: Centralized common imports
global using System;
global using Microsoft.Extensions.Logging;
```

#### 7. Pattern Matching
```csharp
// ✅ GOOD: Advanced patterns in switch expressions
var result = component switch
{
    { Status: ComponentStatus.Active, HasDependencies: true } => ProcessActive(component),
    { Status: ComponentStatus.Inactive } => Skip(component),
    _ => throw new InvalidOperationException()
};
```

### 2.3 Method & Constructor Arguments
- **Single Line**: Keep all arguments on one line
  ```csharp
  // ✅ GOOD
  public Task(string name, string description = "")
  ```
  
- **Exception - Ternary Operators**: Multi-line for clarity when complex
  ```csharp
  // ✅ GOOD: Ternary on multiple lines
  public bool IsValid =>
      Duration > TimeSpan.Zero
          ? ScheduledStartTime.HasValue
          : false;
  ```

---

## 3. CODE QUALITY REQUIREMENTS

### 3.1 Mandatory Standards
- **Null Coalescing**: Use `??` and `?.` operators consistently
- **Pattern Matching**: Use `is null`, `is not null` (never `== null`)
- **Collection Initialization**: Use collection expressions `[]`
- **String Interpolation**: Use `$"..."` over `string.Format()`
- **LINQ**: Prefer method syntax over query syntax
- **Magic Strings**: Forbidden - use constants/configuration exclusively

### 3.2 Comments & Documentation
| Type | When to Use | Example |
|------|------------|---------|
| Why Comments | Explain non-obvious logic | `// Retry 3 times due to known API throttling (see TICKET-123)` |
| What Comments | Forbidden | ❌ `// increment counter` |
| XML Docs | Public API surface only | `/// <summary>Calculates component dependencies</summary>` |

### 3.3 Code Metrics (Hard Requirements)
- **Method Length**: Max 30 lines (excluding comments)
- **Cyclomatic Complexity**: Max 10 per method
- **Constructor Parameter Count**: Max 5 (use value objects for more)
- **Class Responsibilities**: Exactly 1 (SOLID: SRP)

### 3.4 Error Handling
```csharp
// ✅ GOOD: Specific exceptions with context
throw new InvalidOperationException(
    $"Component '{componentId}' cannot transition from {currentStatus} to {newStatus}");

// ❌ AVOID: Generic exceptions, no context
throw new Exception("Error");
```

---

## 4. DESIGN PATTERNS & ARCHITECTURE

### 4.1 Domain-Driven Design (DDD) Structure
```
src/
├── Domain/
│   ├── Aggregates/          # Root entities
│   ├── ValueObjects/        # Immutable value objects
│   ├── Entities/            # Domain entities
│   └── DomainEvents/        # Domain event definitions
├── Application/
│   ├── Services/            # Use cases/workflows
│   ├── Handlers/            # Command/event handlers
│   └── DTOs/                # Data transfer objects
├── Infrastructure/
│   ├── Persistence/         # Repository implementations
│   ├── ExternalServices/    # Third-party integrations
│   └── Configuration/       # Setup & configuration
└── Presentation/            # API/UI entry points
```

### 4.2 Value Objects Pattern
**Required for all identity-like values**

```csharp
// ✅ GOOD: Generic base class prevents primitive obsession
public class ComponentId : Identifier<Guid>
{
    public ComponentId(Guid value) : base(value) { }
}

// ✅ GOOD: Use it
public class Component(ComponentId id, string name) { }

// ❌ AVOID: Primitive obsession
public class Component(Guid componentId, string name) { }
```

### 4.3 Primitive Obsession Avoidance (Critical Rule)

**Definition**: Primitive Obsession is using primitive types (string, int, guid, etc.) directly to represent domain concepts instead of creating appropriate value objects.

**Mandatory Rule**: Every domain concept must be represented by a dedicated value object or record type. Do not pass raw primitives between layers or as domain parameters.

#### 4.3.1 Primitive Types Requiring Value Objects
| Primitive | Domain Concept | Value Object Example |
|-----------|-----------------|----------------------|
| `Guid` | Identity/ID | `ComponentId : Identifier<Guid>` |
| `string` | Email, Phone, Code | `EmailAddress`, `PhoneNumber` |
| `int` | Quantity, Duration | `Quantity`, `RetryCount` |
| `decimal` | Money, Percentage | `Money`, `Percentage` |
| `DateTime` | Business dates | `ScheduledDate`, `CompletionTime` |
| `bool` | Business states | `IsActive`, `HasValidation` |
| `IEnumerable<string>` | Typed collections | `ComponentIds : IReadOnlySet<ComponentId>` |

#### 4.3.2 Anti-Patterns (All Forbidden)

```csharp
// ❌ CRITICAL: Raw string for email
public void SendNotification(string email) { }

// ❌ CRITICAL: Raw Guid for identity
public class Component { public Guid ComponentId { get; set; } }

// ❌ CRITICAL: Raw int for business concept
public void RetryOperation(int retryCount) { }

// ❌ CRITICAL: Raw collection of strings for typed IDs
public class Component { public IReadOnlyList<string> DependencyIds { get; set; } }

// ❌ CRITICAL: Multiple primitives representing one concept
public void ScheduleTask(string taskId, DateTime start, DateTime end, bool isActive) { }
```

#### 4.3.3 Correct Implementations

```csharp
// ✅ GOOD: Value object for email
public record class EmailAddress
{
    public string Value { get; init; }
    
    public EmailAddress(string value)
    {
        if (!IsValidEmail(value))
            throw new ArgumentException("Invalid email format", nameof(value));
        Value = value;
    }
}

// ✅ GOOD: Generic identifier base class
public abstract class Identifier<T>(T value) where T : notnull
{
    public T Value { get; } = value;
    
    public override bool Equals(object obj) =>
        obj is Identifier<T> other && EqualityComparer<T>.Default.Equals(Value, other.Value);
    
    public override int GetHashCode() => Value.GetHashCode();
}

// ✅ GOOD: Typed ID inheriting from base
public class ComponentId : Identifier<Guid>
{
    public ComponentId(Guid value) : base(value) { }
}

// ✅ GOOD: Typed collection of IDs
public record class ComponentDependencies(IReadOnlySet<ComponentId> Dependencies)
{
    public static readonly ComponentDependencies None = new([]);
}

// ✅ GOOD: Domain concept as record
public record class ScheduledTask(
    TaskId Id,
    TaskName Name,
    DateTime ScheduledStart,
    DateTime RequiredEnd,
    bool IsActive);
```

#### 4.3.4 When to Create Value Objects

| Scenario | Action |
|----------|--------|
| Validating input (email, phone, etc.) | ✅ Create value object with validation |
| Representing identity/ID | ✅ Create typed ID class inheriting `Identifier<T>` |
| Business concept with rules | ✅ Create value object with behavior |
| Multiple primitives forming one concept | ✅ Create encapsulating value object |
| Simple transport/DTO | ⚠️ Can use record with primitive properties if truly temporary |
| Internal implementation detail | ⚠️ Can use primitive if scoped to single method |

#### 4.3.5 Value Object Creation Template

```csharp
// Pattern: Follow this template for all new value objects
public record class DomainConcept(string Value)
{
    // Validation in constructor
    public DomainConcept(string value) : this(ValidateAndNormalize(value)) { }
    
    // Implicit conversion from primitive (optional convenience)
    public static implicit operator DomainConcept(string value) => new(value);
    public static implicit operator string(DomainConcept concept) => concept.Value;
    
    private static string ValidateAndNormalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty");
        return value.Trim();
    }
}
```

#### 4.3.6 Refactoring Example

```csharp
// ❌ BEFORE: Primitive obsession
public class ComponentService
{
    public async Task ExecuteAsync(string componentId, IEnumerable<string> dependencyIds, string name)
    {
        foreach (var depId in dependencyIds)
        {
            // Unclear what these strings represent
            await ProcessDependencyAsync(depId);
        }
    }
}

// ✅ AFTER: Proper value objects
public class ComponentService(IComponentRepository repository)
{
    public async Task ExecuteAsync(ComponentId componentId, ComponentDependencies dependencies, ComponentName name)
    {
        foreach (var depId in dependencies.Values)
        {
            // Type system enforces meaning
            await ProcessDependencyAsync(depId);
        }
    }
}
```

#### 4.3.7 Rationale
- **Type Safety**: Compiler prevents mixing IDs, names, codes
- **Self-Documenting**: Method signatures reveal intent
- **Validation**: Encapsulate business rules once, enforce everywhere
- **Testability**: Easy to create test instances with semantics
- **Maintainability**: Changes to validation affect all uses automatically
- **Domain Alignment**: Code mirrors business language

---

## 4.4 SOLID Principles Enforcement
| Principle | Enforcement | Violation Example |
|-----------|------------|-------------------|
| **SRP** | 1 reason to change per class | Mixing persistence + business logic |
| **OCP** | Extend, don't modify | Adding if/else for new behaviors |
| **LSP** | Substitutable derived types | Throwing NotImplementedException in override |
| **ISP** | Segregated interfaces | `IRepository<T>` vs `IReadRepository<T>` + `IWriteRepository<T>` |
| **DIP** | Depend on abstractions | Direct database calls in services (should use repository) |

### 4.5 Dependency Injection Pattern
```csharp
// ✅ GOOD: Constructor injection
public class ComponentService(
    IComponentRepository repository,
    ILogger<ComponentService> logger)
{
    public async Task ExecuteAsync() { }
}

// ❌ AVOID: Service locator pattern
var service = ServiceLocator.GetService<IComponentRepository>();

// ❌ AVOID: Static methods for dependencies
public static class ComponentHelper
{
    public static void Execute() { }
}
```

---

## 5. CODE FORMATTING & STRUCTURE

### 5.1 Type Declarations
```csharp
// Order: namespace → usings → file-scoped class → members
namespace ConsoleApp.Ifx.Models;

using System;
using System.Collections.Generic;

file class ComponentValidator
{
    private readonly ComponentId _id;
    
    public ComponentValidator(ComponentId id) => _id = id;
    
    public bool Validate() => /* logic */;
}
```

### 5.2 Member Ordering (Consistent Organization)
1. Constants & Static Fields
2. Instance Fields
3. Properties (Public → Protected → Private)
4. Constructors
5. Public Methods
6. Protected Methods
7. Private Methods
8. Nested Types

### 5.3 Blank Lines & Spacing
- 2 blank lines between top-level members
- 1 blank line between method logic sections
- No trailing whitespace
- Unix-style line endings (LF)

---

## 6. TESTING REQUIREMENTS

### 6.1 Test Classification
| Type | When | Coverage Target |
|------|------|-----------------|
| **Unit Tests** | Value objects, business logic, validators | >80% |
| **Integration Tests** | Repositories, services, handlers | >70% |
| **Domain Tests** | Aggregate behavior, invariants | 100% |
| **API Tests** | Endpoints, contracts | >60% |

### 6.2 Test Organization
```
tests/
├── ConsoleApp.Domain.Tests/
│   ├── ValueObjects/
│   ├── Aggregates/
│   └── Services/
├── ConsoleApp.Application.Tests/
│   └── Handlers/
└── ConsoleApp.Integration.Tests/
    └── Repositories/
```

### 6.3 Test Naming
```csharp
// Pattern: Given_When_Then or MethodName_Scenario_Expected
[Fact]
public void Execute_WithValidInput_ReturnsSuccess() { }

[Theory]
[InlineData(0)]
[InlineData(-1)]
public void Execute_WithInvalidDuration_ThrowsException(int duration) { }
```

---

## 7. LOGGING & OBSERVABILITY

### 7.1 Logging Standards
```csharp
// ✅ GOOD: Structured logging with context
logger.LogInformation(
    "Processing component {ComponentId} with {DependencyCount} dependencies",
    componentId,
    dependencies.Count);

// ❌ AVOID: Unstructured strings
logger.LogInformation($"Processing component {componentId}");

// ❌ AVOID: Logging exceptions without context
logger.LogError(ex, "Error occurred");

// ✅ GOOD: Full exception context
logger.LogError(ex, "Failed to process component {ComponentId}: {ErrorMessage}", 
    componentId, ex.Message);
```

### 7.2 Log Levels
| Level | Usage |
|-------|-------|
| **Critical** | Unrecoverable application failure |
| **Error** | Recoverable error, operation failed |
| **Warning** | Potentially harmful situation |
| **Information** | Significant application events |
| **Debug** | Detailed diagnostic information |
| **Trace** | Very detailed diagnostic data |

---

## 8. CONFIGURATION & EXTERNAL DEPENDENCIES

### 8.1 Configuration Access
```csharp
// ✅ GOOD: Strongly-typed options
public class ComponentOptions
{
    public required string ApiKey { get; init; }
    public int MaxRetries { get; init; } = 3;
}

services.Configure<ComponentOptions>(configuration.GetSection("Components"));
```

### 8.2 External Service Integration
```csharp
// ✅ GOOD: Abstraction layer
public interface IExternalComponentService
{
    Task<ComponentData> GetComponentAsync(ComponentId id);
}

// ✅ GOOD: Timeout & resilience patterns
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var result = await httpClient.GetAsync(url, cts.Token);
```

---

## 9. ANTI-PATTERNS & FORBIDDEN PRACTICES

| Pattern | Reason | Severity |
|---------|--------|----------|
| **Async without Async suffix** | Method name doesn't match behavior; breaks conventions | 🔴 Critical |
| **Async method without CancellationToken** | Prevents graceful cancellation/timeouts | 🔴 Critical |
| **CancellationToken not used/handled** | Parameter present but ignored; defeats purpose | 🔴 Critical |
| **Wrong CancellationToken parameter name** | Must be `ct` for consistency; not `cancellationToken` | 🔴 Critical |
| **Primitive Obsession** | Raw primitives as domain concepts; prevents type safety | 🔴 Critical |
| Underscore field prefixes | Legacy; use modern properties | 🔴 Critical |
| `Task.Result` / `Task.Wait()` | Causes deadlocks, breaks async | 🔴 Critical |
| Service Locator pattern | Hides dependencies, hard to test | 🔴 Critical |
| Raw Guid/string for identity | Use typed `Identifier<T>` instead | 🔴 Critical |
| Raw int for quantities/counts | Create value object wrapper | 🔴 Critical |
| Collections of raw strings/GUIDs | Use typed collections (e.g., `IReadOnlySet<ComponentId>`) | 🔴 Critical |
| `null == value` instead of `value is null` | Inconsistent with C# idioms | 🟡 High |
| Magic numbers/strings | Unmaintainable, hard to test | 🟡 High |
| Catching generic `Exception` | Masks real errors | 🟡 High |
| Mixed async/sync boundaries | Introduces deadlocks | 🔴 Critical |
| Inheritance for code reuse | Use composition/mixins instead | 🟡 High |

---

## 10. COMMON PATTERNS & IDIOMS

### 10.1 Null-Coalescing Pattern
```csharp
// ✅ GOOD
var result = component?.Name ?? "Unknown";
var config = options?.Value ?? throw new InvalidOperationException();

// ❌ AVOID
var result = component == null ? "Unknown" : component.Name;
```

### 10.2 Guard Clauses
```csharp
// ✅ GOOD: Early exit
public void Execute(Component? component)
{
    if (component is null)
        throw new ArgumentNullException(nameof(component));
    
    // Business logic
}

// ❌ AVOID: Nested conditions
public void Execute(Component component)
{
    if (component != null)
    {
        // Business logic
    }
}
```

### 10.3 Record Deconstruction
```csharp
// ✅ GOOD: Pattern matching
var (id, name) = componentData;
```

---

## 11. CONCURRENCY & PARALLELISM

### 11.1 Async Method Naming & Cancellation Token Requirements (MANDATORY)

**All async methods MUST follow these three rules:**

1. **Async Suffix**: All methods returning `Task` or `Task<T>` MUST end with `Async`
2. **CancellationToken Parameter**: All async methods MUST have `CancellationToken ct` as the last parameter
3. **Cancellation Handling**: All methods receiving `CancellationToken` MUST appropriately handle cancellation

```csharp
// ✅ GOOD: Complete async pattern
public async Task ProcessComponentAsync(ComponentId id, CancellationToken ct)
{
    try
    {
        var component = await repository.GetAsync(id, ct).ConfigureAwait(false);
        await ProcessAsync(component, ct).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        // Don't log cancellation; expected behavior
        throw;
    }
}

// ✅ GOOD: Passing token through call chain
public async Task<Component> GetComponentAsync(ComponentId id, CancellationToken ct)
{
    return await repository.GetAsync(id, ct).ConfigureAwait(false);
}

// ✅ GOOD: Optional parameter with default
public async Task SaveAsync(Component component, CancellationToken ct = default)
{
    await repository.SaveAsync(component, ct).ConfigureAwait(false);
}

// ❌ CRITICAL: Missing Async suffix on async method
public async Task ProcessComponent() {}

// ❌ CRITICAL: Missing CancellationToken parameter
public async Task ProcessComponentAsync(ComponentId id)
{
    await Task.Delay(1000).ConfigureAwait(false);
}

// ❌ CRITICAL: CancellationToken parameter exists but not used/handled
public async Task ProcessComponentAsync(ComponentId id, CancellationToken ct)
{
    await Task.Delay(1000).ConfigureAwait(false); // Should pass ct
}

// ❌ CRITICAL: Wrong parameter name (must be 'ct')
public async Task ProcessComponentAsync(ComponentId id, CancellationToken cancellationToken)
{
    // Non-standard parameter name
}
```

### 11.2 Async/Await Patterns & ConfigureAwait

```csharp
// ✅ GOOD: ConfigureAwait for all awaits
public async Task ExecuteAsync(CancellationToken ct)
{
    var result = await GetDataAsync(ct).ConfigureAwait(false);
    return result;
}

// ✅ GOOD: Pass token through entire call chain
public async Task ProcessAllAsync(IEnumerable<ComponentId> ids, CancellationToken ct)
{
    foreach (var id in ids)
    {
        await ProcessComponentAsync(id, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
    }
}

// ✅ GOOD: Use ct.ThrowIfCancellationRequested() for manual checks
public async Task LongRunningAsync(CancellationToken ct)
{
    for (int i = 0; i < 100; i++)
    {
        ct.ThrowIfCancellationRequested();
        await ProcessBatchAsync(i, ct).ConfigureAwait(false);
    }
}

// ❌ AVOID: sync-over-async (deadlock risk)
var result = GetDataAsync(ct).Result;

// ❌ AVOID: Missing ConfigureAwait(false)
var result = await GetDataAsync(ct);
```

### 11.3 Concurrent Collections (Thread-Safe)
```csharp
// ✅ GOOD: Concurrent collections for multi-threaded scenarios
private readonly ConcurrentDictionary<ComponentId, Component> _cache = new();

// ✅ GOOD: Channel<T> for producer-consumer
var channel = Channel.CreateUnbounded<Task>();
await channel.Writer.WriteAsync(task, ct).ConfigureAwait(false);
var item = await channel.Reader.ReadAsync(ct).ConfigureAwait(false);
```

---

## 12. EXCEPTION HANDLING STRATEGY

### 12.1 Exception Design Pattern
```csharp
// ✅ GOOD: Custom domain exceptions
public class ComponentNotFoundException : DomainException
{
    public ComponentId ComponentId { get; }
    
    public ComponentNotFoundException(ComponentId componentId) 
        : base($"Component '{componentId}' not found")
    {
        ComponentId = componentId;
    }
}
```

### 12.2 Guard Clauses (Early Exit)
```csharp
// ✅ GOOD: Validate at entry point
ArgumentNullException.ThrowIfNull(id, nameof(id));
ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
```

---

## 13. RECORDS VS CLASSES DECISION TREE

| Scenario | Type | Example |
|----------|------|---------|
| **Value Object** (immutable, equality by value) | `record class` | `record class ComponentData(Guid Id, string Name);` |
| **Domain Entity** (mutable, identity-based) | `class` | `public class Component { }` |
| **DTO** (transfer data, no logic) | `record class` | `record class ComponentDto(Guid Id, string Name);` |
| **Small immutable value** | `record struct` | `record struct Money(decimal Amount, string Currency);` |
| **Value wrapper** (single primitive) | `class : Identifier<T>` | `class ComponentId : Identifier<Guid>` |

```csharp
// ✅ GOOD: Record for value object with automatic equality
public record class Component(ComponentId Id, string Name, IReadOnlySet<ComponentId> Dependencies);

// ✅ GOOD: Class for mutable aggregate
public class Task
{
    public TaskId Id { get; }
    public string Name { get; private set; }
    public void UpdateName(string newName) => Name = newName;
}
```

---

## 14. RESOURCE MANAGEMENT & DISPOSAL

### 14.1 IAsyncDisposable Pattern
```csharp
// ✅ GOOD: Use using declaration for automatic disposal
using var repository = new ComponentRepository(connectionString);
await repository.SaveAsync(component);
// Disposed automatically at end of scope

// ✅ GOOD: Implement IAsyncDisposable for async cleanup
public class ComponentRepository : IAsyncDisposable
{
    private readonly DbContext _context;
    
    public async ValueTask DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
    }
}
```

---

## 15. FLUENT API & BUILDER PATTERNS

### 15.1 Fluent Configuration
```csharp
// ✅ GOOD: Fluent builder for complex objects
public class ComponentBuilder
{
    private ComponentId? _id;
    private string? _name;
    
    public ComponentBuilder WithId(ComponentId id) { _id = id; return this; }
    public ComponentBuilder WithName(string name) { _name = name; return this; }
    public Component Build() => new(_id!, _name!);
}

// Usage: var component = new ComponentBuilder().WithId(id).WithName("Name").Build();

// ✅ GOOD: Fluent service registration
services
    .AddScoped<IComponentService, ComponentService>()
    .AddScoped<IComponentRepository, ComponentRepository>();
```

---

## 16. EXTENSION METHODS GUIDELINES

### 16.1 When to Use Extension Methods
```csharp
// ✅ GOOD: Enhance framework types with domain operations
public static class ComponentExtensions
{
    public static bool HasCircularDependency(this IEnumerable<Component> components) => false;
    public static IEnumerable<Component> Active(this IEnumerable<Component> components)
        => components.Where(c => c.IsActive);
}

// ❌ AVOID: Adding logic that belongs on the class
public static void SetName(this Component c, string name) => c.Name = name;
```

---

## 17. DOCUMENTATION STANDARDS

### 17.1 XML Documentation Comments
```csharp
// ✅ GOOD: Public API documentation
/// <summary>Processes component dependencies to determine execution order.</summary>
/// <param name="components">Collection of components to process. Cannot be null.</param>
/// <exception cref="CircularDependencyException">Thrown when circular dependencies detected.</exception>
public async Task ProcessDependenciesAsync(IEnumerable<Component> components)
{
}

// ✅ GOOD: Explain why, not what
/// <summary>
/// Uses ordered index scan instead of full table scan for 10x performance improvement.
/// See PERF-123 for benchmarks.
/// </summary>
public async Task<Component?> GetByNameAsync(string name) { }
```

---

## 18. FEATURE ORGANIZATION & VERTICAL SLICING

### 18.1 Project Structure
```
Features/
├── Components/
│   ├── Domain/
│   │   ├── Component.cs
│   │   └── ComponentId.cs
│   ├── Application/
│   │   ├── CreateComponentHandler.cs
│   │   └── GetComponentQuery.cs
│   ├── Infrastructure/
│   │   └── ComponentRepository.cs
│   └── API/
│       └── ComponentController.cs
└── Scheduling/
    └── [Similar structure]
```

**Benefits**: Independent features, parallel development, clear boundaries, complete feature testing

---

## 19. PERFORMANCE & ALLOCATION PATTERNS

### 19.1 Struct vs Class & ValueTask
```csharp
// ✅ GOOD: Struct for tiny immutable values (< 32 bytes, stack allocation)
public record struct ComponentVersion(int Major, int Minor, int Patch);

// ✅ GOOD: ValueTask to reduce allocations
public ValueTask<Component> GetComponentAsync(ComponentId id)
{
    if (_cache.TryGetValue(id, out var component))
        return new ValueTask<Component>(component);
    return new ValueTask<Component>(FetchFromDbAsync(id));
}

// ✅ GOOD: ArrayPool for temporary buffers
using (var buffer = ArrayPool<byte>.Shared.Rent(1024)) { /* use buffer */ }
```

---

## 20. .NET 10 SPECIFIC GOTCHAS

| Issue | Solution |
|-------|----------|
| **Span<T> lifetime** | Cannot return Span from methods; use Memory<T> |
| **Target-typed new()** | Use with explicit type: `Component component = new();` |
| **Reflection perf** | Use source generators or compiled expressions instead |
| **LINQ deferred execution** | Materialize if collection changes: `var count = items.Count();` then modify |

---

## 21. RELEASE NOTES & CHANGELOG

**v1.3.0** (2025-01-09) ⭐ MAJOR EXPANSION
- ⭐ **NEW**: Split-out pattern guides for AI consumption
  - patterns_ddd.md: Repository, Specification, Domain Events, Aggregate Root
  - patterns_validation.md: Validation, Error Handling, Result Pattern
  - patterns_testing.md: Unit/Integration Testing, Test Data Builders
- ⭐ **NEW**: 8 additional critical rule sections
  - Immutability requirements (init-only, records, readonly collections)
  - Sealed classes (seal unless designed for inheritance)
  - Readonly structs for performance-critical value objects
  - Invariant checking at boundaries
  - Strong typing & type mappings (no primitive obsession)
  - Configuration & Dependency Injection patterns
  - Markdown file naming convention (lowercase)
- Updated to 31 total sections from 22
- File renamed: COPILOT_INSTRUCTIONS_ADDITIONS.md → copilot_instructions_additions.md
- File renamed: ASYNC_REQUIREMENTS_v1.2.0.md → async_requirements_v1.2.0.md

**v1.2.0** (2025-01-09)
- ⭐ **CRITICAL**: Added mandatory async method naming & CancellationToken requirements
  - All async methods MUST use `Async` suffix
  - All async methods MUST have `CancellationToken ct` as last parameter
  - All CancellationToken uses MUST be properly handled
- Added concurrency and exception handling guidelines
- Updated anti-patterns with async violations (critical severity)

**v1.1.0** (2025-01-09)
- Added concurrency and exception handling guidelines
- Expanded records vs classes decision tree
- Resource management and disposal patterns
- Fluent API and builder pattern recommendations
- Extension methods guidelines
- Documentation standards with XML comments
- Feature organization and vertical slicing
- Performance and allocation patterns
- .NET 10 specific gotchas

**v1.0.0** (2025-01-09)
- Initial comprehensive instruction set
- AI-optimized metadata structure
- Technology stack documentation
- Design pattern reference library

---

## 31. QUICK REFERENCE
