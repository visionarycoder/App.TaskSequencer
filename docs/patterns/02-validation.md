---
metadata:
  version: "1.0.0"
  created: "2025-01-09"
  section: "validation_error_handling"
  parent: "copilot_instructions.md"
  target_audience: "AI Code Assistant"

---

# Validation & Error Handling Patterns

## Input Validation Strategy

### Guard Clauses (Entry Point)
```csharp
// ✅ GOOD: Validate parameters immediately
public class ComponentService(IComponentRepository repository)
{
    public async Task UpdateComponentAsync(ComponentId id, string name, CancellationToken ct)
    {
        // Validate all inputs before proceeding
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name.Length > 255)
            throw new ArgumentException("Name cannot exceed 255 characters", nameof(name));

        var component = await repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (component is null)
            throw new ComponentNotFoundException(id);

        component.UpdateName(name);
        await repository.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
```

### Validation Rules at Domain Level
```csharp
// ✅ GOOD: Domain validations in aggregate
public record class ComponentName
{
    public string Value { get; }

    public ComponentName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 255)
            throw new ArgumentException("Name cannot exceed 255 characters", nameof(value));

        Value = value.Trim();
    }

    public static implicit operator string(ComponentName name) => name.Value;
    public static implicit operator ComponentName(string value) => new(value);
}
```

---

## Error Handling Strategy

### Exception Hierarchy
```csharp
// ✅ GOOD: Base domain exception
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception inner) : base(message, inner) { }
}

// ✅ GOOD: Specific domain exceptions
public class ComponentNotFoundException : DomainException
{
    public ComponentId ComponentId { get; }
    public ComponentNotFoundException(ComponentId id) 
        : base($"Component '{id}' not found")
    {
        ComponentId = id;
    }
}

public class InvalidComponentStateException : DomainException
{
    public ComponentStatus CurrentStatus { get; }
    public ComponentStatus RequestedStatus { get; }

    public InvalidComponentStateException(ComponentStatus current, ComponentStatus requested)
        : base($"Cannot transition from {current} to {requested}")
    {
        CurrentStatus = current;
        RequestedStatus = requested;
    }
}

public class CircularDependencyException : DomainException
{
    public IReadOnlyList<ComponentId> CircularPath { get; }

    public CircularDependencyException(IReadOnlyList<ComponentId> path)
        : base($"Circular dependency detected: {string.Join(" → ", path)}")
    {
        CircularPath = path;
    }
}
```

### Structured Exception Handling
```csharp
// ✅ GOOD: Specific exception handling with logging
public async Task ExecuteAsync(ComponentId id, CancellationToken ct)
{
    try
    {
        await ProcessComponentAsync(id, ct).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        // Expected; don't log
        throw;
    }
    catch (ComponentNotFoundException ex)
    {
        _logger.LogWarning(ex, "Component {ComponentId} not found during processing", ex.ComponentId);
        throw;
    }
    catch (DomainException ex)
    {
        _logger.LogError(ex, "Domain error during component processing: {Error}", ex.Message);
        throw;
    }
    catch (DbUpdateException ex)
    {
        _logger.LogError(ex, "Database error during component processing");
        throw new PersistenceException("Failed to save changes", ex);
    }
    catch (Exception ex)
    {
        _logger.LogCritical(ex, "Unexpected error during component processing");
        throw;
    }
}
```

---

## Result Pattern (Success/Failure Handling)

### Generic Result Types
```csharp
// ✅ GOOD: Result without value
public record class Result(bool IsSuccess, string? Error = null)
{
    public static Result Success() => new(true);
    public static Result Failure(string error) => new(false, error);
}

// ✅ GOOD: Result with value
public record class Result<T>(bool IsSuccess, T? Value = default, string? Error = null)
{
    public static Result<T> Success(T value) => new(true, value);
    public static Result<T> Failure(string error) => new(false, default, error);
}

// ✅ GOOD: Chainable Result pattern
public static class ResultExtensions
{
    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
        this Task<Result<TIn>> result,
        Func<TIn, Task<TOut>> mapper,
        CancellationToken ct)
    {
        var res = await result.ConfigureAwait(false);
        if (!res.IsSuccess)
            return Result<TOut>.Failure(res.Error!);

        try
        {
            var mapped = await mapper(res.Value!).ConfigureAwait(false);
            return Result<TOut>.Success(mapped);
        }
        catch (Exception ex)
        {
            return Result<TOut>.Failure(ex.Message);
        }
    }
}
```

### Usage Pattern
```csharp
// ✅ GOOD: Using Result pattern instead of exceptions for expected failures
public async Task<Result<Component>> TryGetComponentAsync(ComponentId id, CancellationToken ct)
{
    try
    {
        var component = await repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        return component is null
            ? Result<Component>.Failure($"Component {id} not found")
            : Result<Component>.Success(component);
    }
    catch (Exception ex)
    {
        return Result<Component>.Failure($"Error retrieving component: {ex.Message}");
    }
}

// ✅ GOOD: Chaining results
public async Task<Result<bool>> ValidateComponentAsync(ComponentId id, CancellationToken ct)
{
    return await TryGetComponentAsync(id, ct)
        .MapAsync(async c => await CheckCircularDependenciesAsync(c, ct), ct)
        .ConfigureAwait(false);
}
```

---

## Validation Rule Engine

### Fluent Validation Pattern
```csharp
// ✅ GOOD: Reusable validation rules
public abstract class ValidationRule<T>
{
    public abstract Task<ValidationResult> ValidateAsync(T entity, CancellationToken ct);
}

// ✅ GOOD: Composed validator
public class ComponentValidator
{
    private readonly List<ValidationRule<Component>> _rules = [];

    public ComponentValidator AddRule(ValidationRule<Component> rule)
    {
        _rules.Add(rule);
        return this;
    }

    public async Task<ValidationResult> ValidateAsync(Component component, CancellationToken ct)
    {
        var errors = new List<string>();

        foreach (var rule in _rules)
        {
            var result = await rule.ValidateAsync(component, ct).ConfigureAwait(false);
            if (!result.IsValid)
                errors.AddRange(result.Errors);
        }

        return new ValidationResult(errors.Count == 0, errors);
    }
}

// ✅ GOOD: Specific rules
public class ComponentNameValidationRule : ValidationRule<Component>
{
    public override Task<ValidationResult> ValidateAsync(Component entity, CancellationToken ct)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(entity.Name))
            errors.Add("Component name is required");
        if (entity.Name?.Length > 255)
            errors.Add("Component name cannot exceed 255 characters");

        return Task.FromResult(new ValidationResult(errors.Count == 0, errors));
    }
}

public class CircularDependencyValidationRule : ValidationRule<Component>
{
    private readonly IComponentRepository _repository;

    public CircularDependencyValidationRule(IComponentRepository repository) => _repository = repository;

    public override async Task<ValidationResult> ValidateAsync(Component entity, CancellationToken ct)
    {
        var hasCircular = await _repository.HasCircularDependenciesAsync(entity.Id, ct).ConfigureAwait(false);
        if (hasCircular)
            return new ValidationResult(false, ["Component has circular dependencies"]);

        return new ValidationResult(true, []);
    }
}
```

---

## Anti-Patterns

| Pattern | Issue | Solution |
|---------|-------|----------|
| Throwing exceptions for flow control | Performance hit, unclear intent | Use Result pattern for expected failures |
| Generic exception handling | Masks real errors | Catch specific exception types |
| Validation scattered across layers | Inconsistent validation | Centralize at domain layer |
| No context in error messages | Hard to debug | Include relevant identifiers and state |
| Swallowing exceptions silently | Hides problems | Log before re-throwing or use Result |
| Validating too late | Invalid state in system | Validate at aggregate constructor |
