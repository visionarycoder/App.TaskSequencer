# Coding Standards

Foundation coding conventions and naming rules for all code.

---

## Table of Contents

1. [Naming Conventions](#naming-conventions)
2. [C# 14+ Language Features](#c-14-language-features)
3. [Method & Constructor Arguments](#method--constructor-arguments)
4. [Type Declarations](#type-declarations)
5. [Member Ordering](#member-ordering)
6. [Formatting & Spacing](#formatting--spacing)

---

## Naming Conventions

### Standard Pattern Table

| Context | Pattern | Valid | Invalid | Example |
|---------|---------|-------|---------|---------|
| **Public Properties** | PascalCase | ✅ | ❌ | `public string Name { get; set; }` |
| **Public Methods** | PascalCase | ✅ | ❌ | `public void Execute()` |
| **Async Methods** | **PascalCase + `Async` suffix** | ✅ | ❌ | `public Task ExecuteAsync(CancellationToken ct)` |
| **CancellationToken** | **Always `ct`** | ✅ | ❌ | `CancellationToken ct` (not `cancellationToken`) |
| **Public Classes** | PascalCase | ✅ | ❌ | `public class ComponentId` |
| **Public Records** | PascalCase | ✅ | ❌ | `public record ComponentData(Guid Id, string Name)` |
| **Local Variables** | camelCase | ✅ | ❌ | `var componentName = "..."` |
| **Method Parameters** | camelCase | ✅ | ❌ | `void Execute(string taskName)` |
| **Constants** | UPPER_SNAKE_CASE | ✅ | ❌ | `const int MAX_RETRIES = 3` |
| **Private Fields** | Property-backed (NO \_) | ✅ | ❌ | `private string name;` |
| **Underscore Prefix** | **Forbidden** | ❌ | ✅ | Never use `_name` or `_componentId` |

### Critical Pattern: No Underscore Prefixes

```csharp
// ✅ CORRECT: No prefix, property-backed
private string name;
private ComponentId componentId;

// ❌ WRONG: Underscore prefix
private string _name;
private ComponentId _componentId;
```

### Critical Pattern: Async Methods

```csharp
// ✅ CORRECT: Async suffix + CancellationToken ct
public async Task ExecuteAsync(CancellationToken ct) { }
public async Task<int> CountAsync(CancellationToken ct) { }

// ❌ WRONG: Missing Async suffix
public async Task Execute(CancellationToken ct) { }

// ❌ WRONG: Missing CancellationToken
public async Task ExecuteAsync() { }

// ❌ WRONG: Wrong parameter name
public async Task ExecuteAsync(CancellationToken cancellationToken) { }
```

---

## C# 14+ Language Features

Use these features in priority order (highest first):

### Priority 1: Primary Constructors

```csharp
// ✅ GOOD: Reduces boilerplate significantly
public class Component(ComponentId componentId, string name, string description = "")
{
    public ComponentId ComponentId { get; } = componentId;
    public string Name { get; } = name;
    public string Description { get; } = description;
}

// ❌ AVOID: Traditional constructor boilerplate
public class Component
{
    private readonly ComponentId _componentId;
    
    public Component(ComponentId componentId, string name)
    {
        _componentId = componentId;
        Name = name;
    }
    
    public string Name { get; }
}
```

### Priority 2: Init-only Properties & Records

```csharp
// ✅ GOOD: Records for immutable value objects
public record ComponentMetadata(Guid Id, string Name, DateTime CreatedAt);
public record struct ComponentCoordinate(int X, int Y);

// ✅ GOOD: Init-only for immutability
public class Task
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";
}

// ❌ AVOID: Mutable properties in domain models
public class Task { public string Name { get; set; } }
```

### Priority 3: Pattern Matching

```csharp
// ✅ GOOD: Advanced pattern matching in switch expressions
var result = component switch
{
    { Status: ComponentStatus.Active, HasDependencies: true } => ProcessActive(component),
    { Status: ComponentStatus.Inactive } => Skip(component),
    _ => throw new InvalidOperationException()
};

// ✅ GOOD: Null pattern matching
var name = component?.Name ?? "Unknown";

// ✅ GOOD: Not null pattern
if (component is not null) { /* process */ }
```

### Priority 4: File-Scoped Types

```csharp
// ✅ GOOD: Internal implementation details
file class ComponentValidator
{
    public bool Validate(Component component) => /* logic */;
}

// Keep public types without the 'file' modifier
public class Component { }
```

### Priority 5: Collection Expressions

```csharp
// ✅ GOOD: Collection expressions
var items = [];
var expanded = [..existingItems, newItem];
var set = new HashSet<int>(collection);

// ❌ AVOID: Traditional initialization
var items = new List<int>();
var expanded = new List<int>(existingItems) { newItem };
```

### Priority 6: Global Usings

```csharp
// Create GlobalUsings.cs at project root
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;
global using Microsoft.Extensions.Logging;
```

---

## Method & Constructor Arguments

### Single Line (Default)

```csharp
// ✅ GOOD: Keep all arguments on one line when reasonable
public Task(string name, string description = "", TaskId? parentId = null)
```

### Multi-line (Long Signatures)

```csharp
// ✅ GOOD: Multi-line for readability when exceeds ~80 chars
public class ProcessingService(
    IComponentRepository componentRepository,
    ITaskRepository taskRepository,
    ILogger<ProcessingService> logger,
    IMetricsCollector metrics)
{
}
```

### Complex Expressions (Ternary)

```csharp
// ✅ GOOD: Multi-line ternary for clarity
public bool IsExecutable =>
    ScheduledStartTime.HasValue && Duration > TimeSpan.Zero
        ? PrerequisitesComplete
        : false;

// ✅ GOOD: Multi-line LINQ
var validTasks = allTasks
    .Where(t => t.IsScheduled)
    .OrderBy(t => t.ScheduledStartTime)
    .ToList();
```

---

## Type Declarations

### File Structure

```csharp
// Order: namespace → usings → file-scoped types → public type
namespace ConsoleApp.Ifx.Models;

using System;
using System.Collections.Generic;
using ConsoleApp.Ifx.Abstractions;

// Internal helpers (use 'file' keyword)
file class ComponentValidator
{
    public bool Validate(ComponentId id) => /* logic */;
}

// Main public type
public record class ComponentData(Guid Id, string Name, int DurationMinutes);
```

---

## Member Ordering (Consistent Organization)

Organizations members in this order:

1. **Constants & Static Fields**
   ```csharp
   public const int DEFAULT_TIMEOUT_SECONDS = 30;
   private static readonly ILogger Logger = /* */;
   ```

2. **Instance Fields** (private only)
   ```csharp
   private readonly ComponentId _id;
   ```

3. **Properties** (Public → Protected → Private → Init-only)
   ```csharp
   public string Name { get; set; }
   protected DateTime CreatedAt { get; private set; }
   private int Retries { get; set; }
   public required string Title { get; init; }
   ```

4. **Constructors** (Primary first, then explicit)
   ```csharp
   public Component(ComponentId id, string name) { }
   ```

5. **Public Methods**
   ```csharp
   public TaskId Execute() { }
   ```

6. **Protected Methods**
   ```csharp
   protected void ValidateState() { }
   ```

7. **Private Methods**
   ```csharp
   private bool IsValid() { }
   ```

8. **Nested Types** (classes, records, enums, interfaces)

---

## Formatting & Spacing

### Blank Lines

- **2 blank lines** between top-level type members (properties, methods)
- **1 blank line** between logical sections within a method
- **No trailing whitespace**
- **Unix-style line endings** (LF, not CRLF)

```csharp
public class Component
{
    private readonly ComponentId _id;

    public string Name { get; set; }


    public Component(ComponentId id, string name)
    {
        _id = id;
        Name = name;
    }


    public void Execute()
    {
        Validate();

        ProcessDependencies();

        UpdateStatus();
    }
}
```

### String Interpolation & Formatting

```csharp
// ✅ GOOD: Use $"..." for string interpolation
logger.LogInformation($"Component {componentId} executed in {elapsed}ms");

// ❌ AVOID: string.Format()
logger.LogInformation(string.Format("Component {0} executed", componentId));

// ✅ GOOD: Multi-line strings
var sql = """
    SELECT ComponentId, Name, Status
    FROM Components
    WHERE Status = @status
    ORDER BY CreatedAt DESC
    """;
```

### Null Coalescing & Null Checking

```csharp
// ✅ GOOD: Null coalescing operators
var name = component?.Name ?? "Unknown";

// ✅ GOOD: Null pattern matching
if (component is null) return;
if (component is not null) { /* process */ }

// ❌ AVOID: Equality with null
if (component == null) { }
```

---

## Summary: Must-Know Rules

| Rule | ✅ Correct | ❌ Wrong | Severity |
|------|-----------|---------|----------|
| Async methods | `ExecuteAsync(CancellationToken ct)` | `Execute()` or `ExecuteAsync()` | 🔴 CRITICAL |
| CancellationToken param | `CancellationToken ct` | `CancellationToken cancellationToken` | 🔴 CRITICAL |
| Field prefix | `private string name;` | `private string _name;` | 🔴 CRITICAL |
| Naming | PascalCase public, camelCase private | `_name`, `publicName`, `componentID` | 🟡 HIGH |
| Collection init | `[]` or `[..items]` | `new List<int>()` | 🟢 MEDIUM |
| Pattern matching | `is null`, `is not null` | `== null` | 🟢 MEDIUM |

---

## See Also

- [Async Requirements](02-async-requirements.md) - MANDATORY async patterns
- [Code Quality & Architecture](03-code-quality-architecture.md) - Design patterns & metrics
