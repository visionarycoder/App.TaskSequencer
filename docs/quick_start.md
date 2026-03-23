---
metadata:
  version: "1.1.0"
  created: "2025-01-09"
  modified: "2025-01-09"
  location: "docs/"
  type: "quick_start"
  target_audience: "Developers"

---

# Quick Start Guide - Copilot Instructions

**⏱️ 5-minute overview of essential rules**

---

## 📍 Location Note

All copilot instruction files have been moved from `.github/` to `docs/` for better access with AI agents.

**Primary file**: `docs/copilot_instructions.md`  
**Navigation**: `docs/index.md`  
**Async reference**: `docs/async_requirements_v1.2.0.md`  

---

## 🚨 Top 3 Rules (MUST KNOW)

### 1️⃣ Async Methods
```csharp
// ✅ CORRECT
public async Task ProcessAsync(ComponentId id, CancellationToken ct)
{
    await SomethingAsync(id, ct).ConfigureAwait(false);
}

// ❌ WRONG
public async Task Process(ComponentId id)  // Missing Async suffix and CancellationToken
```

**Rule**: 
- Async suffix required
- CancellationToken ct as last parameter
- Must be named `ct` (not `cancellationToken`)

---

### 2️⃣ No Primitives for Domain Concepts
```csharp
// ✅ CORRECT
public Task<Component> GetAsync(ComponentId id, CancellationToken ct)

// ❌ WRONG
public Task<Component> GetAsync(Guid id, CancellationToken cancellationToken)
```

**Rule**: Create value objects for:
- IDs → `class ComponentId : Identifier<Guid>`
- Names → `record class ComponentName`
- Amounts → `record class Quantity`
- Dates → `record class ScheduledDate`

---

### 3️⃣ Immutable + Sealed by Default
```csharp
// ✅ CORRECT
public sealed record class Component(ComponentId Id, string Name)
{
    public IReadOnlySet<ComponentId> Dependencies { get; init; } = [];
}

// ❌ WRONG
public class Component
{
    public Guid Id { get; set; }  // Mutable + not sealed + no value object
    public List<Guid> Dependencies { get; set; }  // Mutable collection
}
```

**Rule**:
- Use `record` for value objects
- Use `init` for one-time setups
- Seal classes: `public sealed class...`
- Collections: `IReadOnly*<T>`

---

## 📋 Essential Rules Checklist

Before submitting PR, verify:

- [ ] **All async methods end with `Async`**
- [ ] **All async methods have `CancellationToken ct` parameter**
- [ ] **No `Task.Result` or `Task.Wait()`**
- [ ] **No raw Guid/string/int for domain concepts**
- [ ] **All classes use `sealed` (unless designed for inheritance)**
- [ ] **All collections are `IReadOnly*`**
- [ ] **Constructor validates all invariants**
- [ ] **No underscore prefixes on fields**
- [ ] **Records used for value objects**
- [ ] **Init-only properties for setup**

---

## 🚀 Common Patterns

### Creating a Domain Value Object
```csharp
// File: ComponentId.cs
namespace ConsoleApp.Ifx.Models;

public sealed class ComponentId : Identifier<Guid>
{
    public ComponentId(Guid value) : base(value) { }
}

// Usage
public sealed record class Component(
    ComponentId Id,
    ComponentName Name,
    IReadOnlySet<ComponentId> Dependencies);
```

### Async Method
```csharp
public async Task ProcessAsync(ComponentId id, CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(id);

    try
    {
        var component = await repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (component is null)
            throw new ComponentNotFoundException(id);

        // Do work
        await repository.SaveChangesAsync(ct).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        throw;  // Don't log cancellation
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing component {Id}", id);
        throw;
    }
}
```

### Test
```csharp
[Fact]
public async Task ExecuteAsync_WithValidInput_Succeeds()
{
    // Arrange
    var id = new ComponentId(Guid.NewGuid());
    var repository = new Mock<IComponentRepository>();
    var service = new ComponentService(repository.Object);

    // Act
    var result = await service.ProcessAsync(id, CancellationToken.None);

    // Assert
    Assert.NotNull(result);
}
```

---

## 📚 Where to Find More

| Question | Answer |
|----------|--------|
| Where's the full guide? | `copilot_instructions.md` |
| Need pattern examples? | `patterns_*.md` files |
| How do I test this? | `patterns_testing.md` |
| What's the navigation? | `index.md` |
| Anti-patterns? | `copilot_instructions.md` §9 |

---

## ⚠️ Common Mistakes

### ❌ Missing Async Pattern
```csharp
public async Task LoadData()  // Missing: Async suffix, CancellationToken
{
    await Task.Delay(1000);
}
```

✅ **Fix**: 
```csharp
public async Task LoadDataAsync(CancellationToken ct)
{
    await Task.Delay(1000, ct).ConfigureAwait(false);
}
```

### ❌ Raw Primitives
```csharp
public async Task UpdateAsync(Guid componentId, string name, int status)
```

✅ **Fix**:
```csharp
public async Task UpdateAsync(ComponentId id, ComponentName name, ComponentStatus status, CancellationToken ct)
```

### ❌ Mutable Properties
```csharp
public class Component
{
    public string Name { get; set; }  // Mutable!
    public List<Guid> Tags { get; set; }  // Mutable collection!
}
```

✅ **Fix**:
```csharp
public sealed record class Component(
    ComponentId Id,
    ComponentName Name,
    IReadOnlySet<string> Tags);
```

### ❌ Not Sealed
```csharp
public class ComponentService  // Not sealed - anyone can inherit
{
    public virtual void Process()  // Virtual method
    {
    }
}
```

✅ **Fix**:
```csharp
public sealed class ComponentService
{
    public void Process() { }  // Not virtual
}
```

---

## 🤔 Quick Decision Trees

### Should I create a value object for this type?
```
Is it a business concept? 
├─ YES → Create value object ✅
└─ NO → OK to use primitive ⚠️

Does it need validation?
├─ YES → Create value object ✅
└─ NO → Consider using primitive ⚠️

Is it an ID/identifier?
├─ YES → ALWAYS create value object ✅
└─ NO → Check above questions
```

### Should I use `record`, `class`, or `record struct`?
```
Is it a value object (immutable, equality by value)?
├─ YES, small (< 32 bytes) → record struct ✅
├─ YES, large (> 32 bytes) → record class ✅
└─ NO (entity, needs mutations) → class ✅

Should instances be mutable?
├─ YES → class ⚠️
├─ Sometimes → class with init ✅
└─ NO → record ✅
```

### When should I use `sealed`?
```
Is this class designed for inheritance?
├─ EXPLICITLY YES → public class (with virtual methods)
└─ NO → public sealed class ✅

Any virtual methods?
├─ YES → Consider if inheritance is intentional
└─ NO → Always seal ✅
```

---

## 📞 Getting Help

- **Rule question?** Search `copilot_instructions.md` for section number
- **Pattern question?** Check `patterns_*.md` files
- **Test question?** See `patterns_testing.md`
- **Navigation?** Go to `index.md` → FAQ

---

## 📝 Before You Submit

**Code Review Checklist**:

1. ✅ Run compiler - no errors
2. ✅ Run tests - all passing
3. ✅ Check async methods:
   - [ ] Has `Async` suffix
   - [ ] Has `CancellationToken ct` parameter
   - [ ] Uses `ConfigureAwait(false)`
4. ✅ Check domain objects:
   - [ ] No raw GUIDs
   - [ ] No raw strings for business concepts
   - [ ] No mutable properties
5. ✅ Check classes:
   - [ ] Are sealed (unless intentionally designed otherwise)
   - [ ] Invariants validated in constructor
6. ✅ Check collections:
   - [ ] All are `IReadOnly*`

---

**Ready to code?** Start with your domain model using value objects, then implement async methods with proper CancellationToken handling. 🚀
