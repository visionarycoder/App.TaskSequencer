# Coding Standards & Code Quality

Documentation covering naming conventions, code formatting, and code quality rules.

---

## 📄 Documents in This Section

### 1. [Coding Standards](01-coding-standards.md)
**Read First**: ✅ YES  
**Purpose**: How to write code that follows the project style

**Contains**:
- Naming conventions (PascalCase, camelCase, UPPER_SNAKE_CASE for constants)
- **Async method naming requirements** (Async suffix + `CancellationToken ct` parameter) - MANDATORY
- C# 14+ language features by priority
- Method & constructor argument formatting rules
- Type declarations and member ordering
- Blank lines & spacing conventions

**Key Rules**:
- ❌ No underscore prefix for private fields
- ✅ Property-backed fields
- ✅ Async methods MUST have `Async` suffix
- ✅ CancellationToken parameter MUST be named `ct`
- ✅ Use primary constructors
- ✅ Init-only properties for immutability
- ✅ Records for value objects & DTOs

**When to Read**:
- Before writing any code
- Setting up IDE formatting rules
- Code review feedback reference

---

### 2. [Async Requirements (MANDATORY)](02-async-requirements.md)
**Read First**: ✅ YES (if writing async code)  
**Purpose**: Critical async/await patterns and requirements

**Contains**:
- Three MANDATORY async requirements
- ConfigureAwait(false) rules
- CancellationToken patterns & examples
- Forbidden patterns (async without suffix, missing cancellation token)
- Critical severity violations

**Critical Rules** (Violations are breaking):
- ❌ Async methods MUST end with `Async` suffix
- ❌ Missing `CancellationToken ct` parameter
- ❌ Wrong parameter name (e.g., `cancellationToken` instead of `ct`)
- ✅ Handle `OperationCanceledException` gracefully

**When to Read**:
- Writing async methods
- Code review of async code
- Understanding cancellation patterns

---

### 3. [Code Quality & Architecture](03-code-quality-architecture.md)
**Read First**: ✅ YES (for code organization)  
**Purpose**: Quality metrics, design patterns, and DDD structure

**Contains**:
- Code quality metrics (hard limits)
- Error handling patterns
- **Primitive Obsession avoidance** (CRITICAL - see details below)
- Domain-Driven Design structure
- Value objects pattern
- SOLID principles
- Dependency injection
- Comments & documentation standards

**Key Rules**:
- ❌ **Primitive Obsession**: Use raw primitives only in specific cases
- ✅ Create value objects for all domain concepts (emails, IDs, codes, etc.)
- ✅ Every domain concept must be a dedicated type
- ✅ Use typed IDs (`ComponentId : Identifier<Guid>`)
- Max 30 lines per method
- Max cyclomatic complexity of 10
- Exactly 1 reason to change per class (SRP)

**Primitive Obsession Quick Reference**:
| Primitive | ❌ Anti-Pattern | ✅ Solution |
|-----------|----------------|-----------|
| `Guid` for ID | `public void Process(Guid id)` | `public void Process(ComponentId id)` |
| `string` for email | `public void Send(string email)` | `public void Send(EmailAddress email)` |
| `int` for quantity | `public void Order(int qty)` | `public void Order(Quantity qty)` |
| `IEnumerable<string>` for IDs | `IEnumerable<string> deps` | `IReadOnlySet<ComponentId> deps` |

**When to Read**:
- Designing domain models
- Code review for quality metrics
- Understanding value object patterns
- Architecting new features

---

## 🎯 Quick Navigation by Task

| Task | Document |
|------|----------|
| "How do I name things?" | 01-coding-standards.md |
| "What about async methods?" | 02-async-requirements.md (MANDATORY) |
| "Should I use string or a value object?" | 03-code-quality-architecture.md (Primitive Obsession) |
| "How long can my methods be?" | 03-code-quality-architecture.md (Code Metrics) |
| "Should I throw exceptions?" | 03-code-quality-architecture.md (Error Handling) |
| "What about dependency injection?" | 03-code-quality-architecture.md (DIP) |

---

## ⚠️ Critical Rules at a Glance

### CRITICAL RULE #1: Async Naming & Cancellation
```csharp
// ✅ CORRECT
public async Task ExecuteAsync(CancellationToken ct) { }

// ❌ CRITICAL VIOLATION: Missing Async suffix
public async Task Execute(CancellationToken ct) { }

// ❌ CRITICAL VIOLATION: Missing CancellationToken
public async Task ExecuteAsync() { }

// ❌ CRITICAL VIOLATION: Wrong parameter name
public async Task ExecuteAsync(CancellationToken cancellationToken) { }
```

### CRITICAL RULE #2: No Primitive Obsession
```csharp
// ✅ CORRECT: Typed values
public void SendNotification(EmailAddress to, TaskId taskId) { }

// ❌ CRITICAL VIOLATION: Primitive obsession
public void SendNotification(string email, string taskId) { }
```

### CRITICAL RULE #3: No Underscore Prefixes
```csharp
// ✅ CORRECT: Property-backed
private string name;

// ❌ WRONG: Underscore prefix
private string _name;
```

---

## 📊 Code Quality Metrics (Hard Limits)

| Metric | Limit |
|--------|-------|
| Method length | 30 lines max |
| Cyclomatic complexity | 10 max |
| Constructor parameters | 5 max |
| Class responsibilities | 1 (SRP) |

---

## 🔗 Related Documentation

- [Business Requirements](../business/README.md) - Understand the domain first
- [Patterns](../patterns/README.md) - How to apply these standards in complex scenarios
- [Execution Planning](../business/02-execution-sequencing-pipeline.md) - Domain examples
