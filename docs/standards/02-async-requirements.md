# Copilot Instructions - v1.2.0 Async Requirements Update

## Critical Addition: Mandatory Async Patterns

### What Was Added
Your team's specific async method requirements have been incorporated as **critical mandatory rules**:

#### Three Core Requirements (All MANDATORY)

```csharp
// Requirement 1: Async Suffix
public async Task ExecuteAsync(...) { }  // ✅ GOOD
public async Task Execute(...) { }       // ❌ CRITICAL VIOLATION

// Requirement 2: CancellationToken as Last Parameter (must be 'ct')
public async Task ExecuteAsync(ComponentId id, CancellationToken ct) { }  // ✅ GOOD
public async Task ExecuteAsync(ComponentId id) { }                        // ❌ CRITICAL VIOLATION
public async Task ExecuteAsync(ComponentId id, CancellationToken cancellationToken) { }  // ❌ WRONG NAME

// Requirement 3: Handle Cancellation Appropriately
public async Task ExecuteAsync(ComponentId id, CancellationToken ct)
{
    try
    {
        await Task.Delay(1000, ct).ConfigureAwait(false);  // ✅ Pass token
        ct.ThrowIfCancellationRequested();                 // ✅ Check for cancellation
    }
    catch (OperationCanceledException)
    {
        // ✅ Handle gracefully
        throw;
    }
}
```

### Documentation Locations in copilot_instructions.md

**1. Coding Standards (Section 2.1)**
- Added to naming conventions table
- Async Methods: `PascalCase + Async suffix`
- CancellationToken Parameter: Always `ct`

**2. Concurrency & Parallelism (Section 11)**
- Section 11.1: "Async Method Naming & CancellationToken Requirements (MANDATORY)"
- Full requirements with complete examples
- Critical violations highlighted

**3. Anti-Patterns (Section 9)**
- Listed as **4 CRITICAL severity violations**:
  - Async without Async suffix
  - Async method without CancellationToken
  - CancellationToken not used/handled
  - Wrong CancellationToken parameter name (must be `ct`)

**4. Quick Reference (Section 22)**
- Highlighted in key metrics section
- Emergency escalation for async/concurrency issues

### Anti-Patterns Added (All 🔴 CRITICAL)

| Violation | Impact |
|-----------|--------|
| Async without Async suffix | Method name doesn't match behavior; breaks conventions |
| Async method without CancellationToken | Prevents graceful cancellation/timeouts |
| CancellationToken not used/handled | Parameter present but ignored; defeats purpose |
| Wrong CancellationToken parameter name | Must be `ct` for consistency; not `cancellationToken` |

### Examples Now in File

#### ✅ GOOD Patterns (All included)
```csharp
// Complete async pattern
public async Task ProcessComponentAsync(ComponentId id, CancellationToken ct)
{
    try
    {
        var component = await repository.GetAsync(id, ct).ConfigureAwait(false);
        await ProcessAsync(component, ct).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        throw;  // Don't log cancellation; expected behavior
    }
}

// Passing token through call chain
public async Task<Component> GetComponentAsync(ComponentId id, CancellationToken ct)
{
    return await repository.GetAsync(id, ct).ConfigureAwait(false);
}

// Optional parameter with default
public async Task SaveAsync(Component component, CancellationToken ct = default)
{
    await repository.SaveAsync(component, ct).ConfigureAwait(false);
}

// Manual cancellation checks
public async Task LongRunningAsync(CancellationToken ct)
{
    for (int i = 0; i < 100; i++)
    {
        ct.ThrowIfCancellationRequested();
        await ProcessBatchAsync(i, ct).ConfigureAwait(false);
    }
}
```

#### ❌ CRITICAL Violations (All documented)
- Missing Async suffix
- Missing CancellationToken parameter
- CancellationToken parameter exists but not used
- Wrong parameter name (not `ct`)
- Missing ConfigureAwait(false)
- sync-over-async (.Result, .Wait())

### Metadata Updates

**File**: `.github/copilot_instructions.md`
**Version**: 1.2.0
**Modified**: 2025-01-09

**Critical Rules** (now tracked in metadata):
- Async method naming & CancellationToken requirements ⭐
- Primitive obsession avoidance
- No underscore field prefixes

### Impact

✅ **Copilot Integration**: GitHub Copilot will now reference these patterns when suggesting async code  
✅ **IDE Awareness**: Visual Studio will use this for inline suggestions  
✅ **Team Consistency**: Enforced naming and cancellation handling across all async methods  
✅ **Maintainability**: Clear expectations for all async operations  
✅ **Performance**: Proper cancellation prevents resource waste  

### Next Steps for Your Team

1. **Existing Code Review**: Audit all async methods for compliance
2. **Pre-commit Hooks**: Consider linting rules to enforce `Async` suffix
3. **PR Template**: Reference section 11 in code review guidelines
4. **Onboarding**: New team members should read section 11 first

### Usage in Code Reviews

When reviewing PRs, reference:
```
❌ Violation: Missing CancellationToken
See: .github/copilot_instructions.md Section 11.1

✅ Fixed: Proper async pattern
Example: .github/copilot_instructions.md Section 11.1 "GOOD" code sample
```

---

**Version**: 1.2.0  
**Updated**: 2025-01-09  
**File**: `.github/copilot_instructions.md`
