# Quick Reference Card

## 🎯 Documentation at a Glance

### Start Here
👉 **`README.md`** - All core rules (31 sections, 30.5 KB)

### By Task

| Task | File | Section |
|------|------|---------|
| Starting new feature | README.md | §1-4 |
| Writing async code | async_requirements.md | All sections |
| Adding validation | patterns_validation.md | All sections |
| Building domain model | patterns_ddd.md | All sections |
| Writing tests | patterns_testing.md | All sections |
| Code review | README.md | §9 (Anti-patterns) |
| Understanding changes | CHANGELOG.md | All versions |
| Finding documentation | index.md | Navigation guide |
| New contributor | quick_start.md | Setup guide |

---

## ⚠️ Critical Rules (MUST FOLLOW)

### 1. Async Methods 🔴
```csharp
// ✅ GOOD
public async Task ExecuteAsync(CancellationToken ct)
{
    await GetDataAsync(ct).ConfigureAwait(false);
}

// ❌ WRONG - Missing Async suffix
public async Task Execute(CancellationToken ct) { }

// ❌ WRONG - Missing CancellationToken
public async Task ExecuteAsync() { }

// ❌ WRONG - CancellationToken not used
public async Task ExecuteAsync(CancellationToken ct)
{
    await GetDataAsync().ConfigureAwait(false);
}
```

### 2. No Underscore Prefixes 🔴
```csharp
// ✅ GOOD
private string name;

// ❌ WRONG
private string _name;
```

### 3. Primitive Obsession 🔴
```csharp
// ✅ GOOD
public class Component(ComponentId id, ComponentName name) { }

// ❌ WRONG - Raw Guid
public class Component(Guid componentId, string name) { }

// ✅ GOOD - Typed ID
public class ComponentId : Identifier<Guid> { }
```

### 4. Immutability by Default 🔴
```csharp
// ✅ GOOD
public record class Component(ComponentId Id, string Name);
public class Service { public required string Name { get; init; } }

// ❌ WRONG
public class Component { public string Name { get; set; } }
```

---

## 📋 Naming Conventions

| Context | Pattern | Example |
|---------|---------|---------|
| Public Properties | PascalCase | `public string Name { get; set; }` |
| Private Fields | camelCase (no prefix) | `private string name;` |
| **Async Methods** | **PascalCase + Async** | **`public Task ExecuteAsync()`** |
| **CancellationToken** | **Always `ct`** | **`CancellationToken ct`** |
| Constants | UPPER_SNAKE_CASE | `const int MAX_RETRIES = 3` |
| Local Variables | camelCase | `var componentName = "...";` |

---

## 🏗️ C# 14+ Features

| Feature | Use | Example |
|---------|-----|---------|
| Primary Constructors | Classes/records | `public class Component(string id, string name)` |
| Init-only Properties | Immutable | `public string Name { get; init; }` |
| Record Types | Value objects | `public record class ComponentData(Guid Id);` |
| Collection Expressions | Empty/spread | `var items = []; var expanded = [..items, new()];` |
| Switch Expressions | Pattern matching | `result = x switch { Type.A => HandleA(), _ => default };` |
| File-scoped Types | Internal impl | `file class Helper { }` |
| Pattern Matching | Type checks | `if (obj is not null) { }` |
| Null Coalescing | Default values | `var x = obj?.Property ?? defaultValue;` |

---

## 🔍 Anti-Patterns (NEVER DO)

| Pattern | Why | Severity |
|---------|-----|----------|
| Async without `Async` suffix | Non-standard naming | 🔴 CRITICAL |
| Async without `CancellationToken` | No cancellation/timeout | 🔴 CRITICAL |
| `CancellationToken` not used | Parameter exists but ignored | 🔴 CRITICAL |
| Underscore field prefixes | Legacy convention | 🔴 CRITICAL |
| `Task.Result` / `Task.Wait()` | Deadlock risk | 🔴 CRITICAL |
| Primitive obsession | Type-unsafe, confusing | 🔴 CRITICAL |
| Service locator pattern | Hidden dependencies | 🔴 CRITICAL |
| `null == value` | Wrong C# idiom | 🟡 HIGH |
| Magic strings | Unmaintainable | 🟡 HIGH |
| Catching generic `Exception` | Masks real errors | 🟡 HIGH |

---

## 📊 Testing Requirements

| Type | Coverage | When |
|------|----------|------|
| Unit | >80% | Business logic, value objects |
| Integration | >70% | Repositories, services |
| Domain | 100% | Aggregate behavior |
| API | >60% | Endpoints |

---

## 💾 SOLID Principles Quick Check

| Principle | In Code | Violation |
|-----------|---------|-----------|
| **SRP** | 1 responsibility | Mixing persistence + logic |
| **OCP** | Extend, don't modify | Adding if/else for new types |
| **LSP** | Substitute derived types | NotImplementedException in override |
| **ISP** | Small interfaces | Fat interface forcing unused impl |
| **DIP** | Depend on abstractions | Direct DB calls in services |

---

## 🔐 Guard Clauses

```csharp
// ✅ GOOD: Early exit, guard clauses
public void Execute(Component? component)
{
    if (component is null)
        throw new ArgumentNullException(nameof(component));

    // Business logic
}

// ✅ GOOD: Specific validation
ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
ArgumentNullException.ThrowIfNull(id, nameof(id));
```

---

## 📚 Documentation Standards

```csharp
// ✅ GOOD: Document why, not what
/// <summary>
/// Uses ordered index scan for 10x performance improvement.
/// See PERF-123 for benchmarks.
/// </summary>
public async Task<Component?> GetByNameAsync(string name) { }

// ❌ WRONG: What is obvious from code
/// <summary>Increments counter</summary>
public void IncrementCounter() => counter++;
```

---

## 🧪 Test Naming Pattern

```csharp
// Pattern: Given_When_Then or MethodName_Scenario_Expected
[Fact]
public void Execute_WithValidInput_ReturnsSuccess() { }

[Theory]
[InlineData(-1)]
public void Execute_WithInvalidDuration_ThrowsException(int duration) { }
```

---

## 📁 File Organization

```
Features/
├── Domain/
│   ├── ComponentId.cs        (value object/identifier)
│   ├── Component.cs          (aggregate root)
│   └── DomainEvents/
├── Application/
│   ├── CreateComponentHandler.cs    (use case)
│   └── ComponentQueryDto.cs         (DTO)
├── Infrastructure/
│   └── ComponentRepository.cs       (persistence)
└── API/
    └── ComponentController.cs       (entry point)
```

---

## 🚀 Async/Await Checklist

- [ ] Method name ends with `Async`
- [ ] Last parameter is `CancellationToken ct`
- [ ] All awaits include `.ConfigureAwait(false)`
- [ ] All awaits pass the `CancellationToken`
- [ ] Exceptions properly logged (not swallowed)
- [ ] No `Task.Result` / `Task.Wait()`

---

## 🎯 Design Pattern Selection

| Need | Pattern | File |
|------|---------|------|
| Uniquely identify entity | `Identifier<Guid>` | README.md §4.3 |
| Immutable value concept | `record class` | README.md §13 |
| Mutable aggregate | `class` | README.md §13 |
| Business rules | Value object | patterns_ddd.md |
| Type validation | Value object constructor | patterns_validation.md |
| Handle change | Domain event | patterns_ddd.md |
| Fetch data | Repository | patterns_ddd.md |

---

## 🔗 Cross-Reference Quick Links

| Topic | Primary | Backup |
|-------|---------|--------|
| Async | async_requirements.md | README.md §11 |
| Validation | patterns_validation.md | README.md §12 |
| Testing | patterns_testing.md | README.md §6 |
| DDD | patterns_ddd.md | README.md §4 |
| Performance | README.md §19 | README.md §20 |
| Exceptions | README.md §12 | patterns_validation.md |

---

## 📌 Version Info

**Current**: v1.3.1 (2025-01-09)

### What Changed
- ✅ `copilot_instructions.md` → `README.md`
- ✅ `copilot_instructions_additions.md` removed
- ✅ `CHANGELOG.md` added for version tracking
- ✅ `index.md` enhanced with navigation

See `CHANGELOG.md` for complete version history.

---

## 🆘 Common Mistakes

### Mistake 1: Wrong Async Pattern
```csharp
// ❌ WRONG
public Task ProcessAsync(Component c) { return Task.CompletedTask; }

// ✅ CORRECT
public Task ProcessAsync(Component c, CancellationToken ct) 
    => Task.CompletedTask;
```

### Mistake 2: Primitive Obsession
```csharp
// ❌ WRONG
void SendNotification(string email, string phoneNumber) { }

// ✅ CORRECT
void SendNotification(EmailAddress email, PhoneNumber phone) { }
```

### Mistake 3: Missing Guard Clause
```csharp
// ❌ WRONG
public bool Execute(Component? component)
{
    if (component != null)
    {
        // logic
        return true;
    }
    return false;
}

// ✅ CORRECT
public bool Execute(Component? component)
{
    if (component is null)
        throw new ArgumentNullException(nameof(component));

    // logic
    return true;
}
```

### Mistake 4: Wrong Collection Expression
```csharp
// ❌ WRONG - No target type
var items = [];

// ✅ CORRECT - Explicit type
List<Component> items = [];
```

---

## 📞 Where to Find Help

| Need | File |
|------|------|
| Quick answer | This card + README.md table of contents |
| Detailed guidance | Pattern files (patterns_*.md) |
| Version history | CHANGELOG.md |
| Navigation | index.md |
| Specific topic | Use index.md cross-reference table |

---

**Last Updated**: 2025-01-09  
**Quick Reference Version**: 1.0  
**Keep this card handy!** ⭐

For detailed information, see the full documentation in `docs/README.md`
