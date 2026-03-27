---
metadata:
  version: "1.0.0"
  created: "2025-01-09"
  section: "testing_patterns"
  parent: "copilot_instructions.md"
  target_audience: "AI Code Assistant"

---

# Testing Patterns & Guidelines

## Unit Test Structure

### Naming Convention (AAA Pattern)
```csharp
// ✅ GOOD: Arrange-Act-Assert with clear names
[Fact]
public async Task ExecuteAsync_WithValidInput_ReturnsSuccess()
{
    // Arrange
    var input = new ComponentId(Guid.NewGuid());
    var service = new ComponentService(mockRepository);

    // Act
    var result = await service.GetComponentAsync(input, CancellationToken.None);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(input, result.Id);
}

// ✅ GOOD: Theory with multiple test cases
[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
public async Task UpdateNameAsync_WithInvalidName_ThrowsException(string invalidName)
{
    var component = CreateValidComponent();

    var ex = await Assert.ThrowsAsync<ArgumentException>(
        () => component.UpdateNameAsync(invalidName, CancellationToken.None));

    Assert.Equal("name", ex.ParamName);
}
```

### Test Fixtures
```csharp
// ✅ GOOD: Reusable test fixtures
public class ComponentServiceTests : IDisposable
{
    private readonly MockRepository _mockRepository;
    private readonly MockLogger _mockLogger;
    private readonly ComponentService _service;

    public ComponentServiceTests()
    {
        _mockRepository = new MockRepository();
        _mockLogger = new MockLogger();
        _service = new ComponentService(_mockRepository, _mockLogger);
    }

    [Fact]
    public async Task GetAsync_WithValidId_ReturnsComponent()
    {
        var id = new ComponentId(Guid.NewGuid());
        var component = CreateValidComponent(id);
        _mockRepository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(component);

        var result = await _service.GetAsync(id, CancellationToken.None);

        Assert.Equal(component, result);
    }

    public void Dispose()
    {
        _mockRepository.Dispose();
    }

    private Component CreateValidComponent(ComponentId? id = null) =>
        new(id ?? new ComponentId(Guid.NewGuid()), "Test Component", new HashSet<ComponentId>());
}
```

---

## Domain Logic Testing

### Value Object Testing
```csharp
// ✅ GOOD: Comprehensive value object tests
public class ComponentNameTests
{
    [Fact]
    public void Constructor_WithValidName_Succeeds()
    {
        var name = new ComponentName("Valid Name");
        Assert.Equal("Valid Name", name.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidName_Throws(string? invalidName)
    {
        Assert.Throws<ArgumentException>(() => new ComponentName(invalidName!));
    }

    [Fact]
    public void Constructor_WithTrimming_RemovesWhitespace()
    {
        var name = new ComponentName("  Trimmed Name  ");
        Assert.Equal("Trimmed Name", name.Value);
    }

    [Fact]
    public void Equality_WithSameValue_IsEqual()
    {
        var name1 = new ComponentName("Same");
        var name2 = new ComponentName("Same");
        Assert.Equal(name1, name2);
    }

    [Fact]
    public void ImplicitConversion_FromString_Works()
    {
        ComponentName name = "Test";
        Assert.Equal("Test", name.Value);
    }
}
```

### Aggregate Root Testing
```csharp
// ✅ GOOD: Test aggregate invariants and domain events
public class ComponentAggregateTests
{
    [Fact]
    public void ChangeStatus_WithValidTransition_RaisesEvent()
    {
        var component = new Component(
            new ComponentId(Guid.NewGuid()),
            "Test",
            new HashSet<ComponentId>());

        component.ChangeStatus(ComponentStatus.Active);

        var @event = Assert.Single(component.Events);
        Assert.IsType<ComponentStatusChangedEvent>(@event);
        Assert.Equal(ComponentStatus.Active, ((ComponentStatusChangedEvent)@event).NewStatus);
    }

    [Fact]
    public void ChangeStatus_WithSameStatus_DoesNotRaiseEvent()
    {
        var component = new Component(
            new ComponentId(Guid.NewGuid()),
            "Test",
            new HashSet<ComponentId>()) { Status = ComponentStatus.Active };

        component.ChangeStatus(ComponentStatus.Active);

        Assert.Empty(component.Events);
    }
}
```

---

## Integration Testing

### Repository Testing
```csharp
// ✅ GOOD: Integration tests with test database
public class ComponentRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly DbContext _context;
    private readonly ComponentRepository _repository;

    public async Task InitializeAsync()
    {
        _context = new InMemoryDbContext();
        _repository = new ComponentRepository(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_WithNewComponent_Persists()
    {
        var component = new Component(
            new ComponentId(Guid.NewGuid()),
            "Test",
            new HashSet<ComponentId>());

        await _repository.AddAsync(component, CancellationToken.None);
        await _repository.SaveChangesAsync(CancellationToken.None);

        var retrieved = await _repository.GetByIdAsync(component.Id, CancellationToken.None);
        Assert.NotNull(retrieved);
        Assert.Equal(component.Id, retrieved.Id);
    }
}
```

### Service Testing with Mocks
```csharp
// ✅ GOOD: Service tests with mocked dependencies
public class ComponentServiceIntegrationTests
{
    private readonly Mock<IComponentRepository> _mockRepository;
    private readonly Mock<ILogger<ComponentService>> _mockLogger;
    private readonly Mock<IDomainEventPublisher> _mockPublisher;
    private readonly ComponentService _service;

    public ComponentServiceIntegrationTests()
    {
        _mockRepository = new Mock<IComponentRepository>();
        _mockLogger = new Mock<ILogger<ComponentService>>();
        _mockPublisher = new Mock<IDomainEventPublisher>();
        _service = new ComponentService(_mockRepository.Object, _mockLogger.Object, _mockPublisher.Object);
    }

    [Fact]
    public async Task UpdateComponentAsync_WithValidInput_PublishesEvent()
    {
        var id = new ComponentId(Guid.NewGuid());
        var component = new Component(id, "Old Name", new HashSet<ComponentId>());

        _mockRepository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(component);

        await _service.UpdateComponentNameAsync(id, "New Name", CancellationToken.None);

        _mockPublisher.Verify(
            p => p.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
```

---

## Async Testing

### CancellationToken Testing
```csharp
// ✅ GOOD: Test cancellation handling
[Fact]
public async Task ExecuteAsync_WithCancellation_ThrowsOperationCanceledException()
{
    using var cts = new CancellationTokenSource();
    var service = new ComponentService(mockRepository);

    cts.CancelAfter(TimeSpan.FromMilliseconds(10));

    await Assert.ThrowsAsync<OperationCanceledException>(
        () => service.LongRunningOperationAsync(cts.Token));
}

// ✅ GOOD: Test timeout behavior
[Fact]
public async Task ExecuteAsync_WithTimeout_Cancels()
{
    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
    var service = new ComponentService(mockRepository);

    await Assert.ThrowsAsync<OperationCanceledException>(
        () => service.ProcessAsync(id, cts.Token));
}
```

---

## Test Utilities & Builders

### Test Data Builders
```csharp
// ✅ GOOD: Fluent test data builders
public class ComponentBuilder
{
    private ComponentId _id = new(Guid.NewGuid());
    private string _name = "Test Component";
    private ComponentStatus _status = ComponentStatus.Inactive;
    private IReadOnlySet<ComponentId> _dependencies = new HashSet<ComponentId>();

    public ComponentBuilder WithId(ComponentId id) { _id = id; return this; }
    public ComponentBuilder WithName(string name) { _name = name; return this; }
    public ComponentBuilder WithStatus(ComponentStatus status) { _status = status; return this; }
    public ComponentBuilder WithDependencies(params ComponentId[] deps) { _dependencies = new HashSet<ComponentId>(deps); return this; }

    public Component Build() => new(_id, _name, _dependencies) { Status = _status };
}

// Usage
[Fact]
public void Test_WithCustomComponent()
{
    var component = new ComponentBuilder()
        .WithName("Custom")
        .WithStatus(ComponentStatus.Active)
        .Build();

    Assert.Equal("Custom", component.Name);
    Assert.Equal(ComponentStatus.Active, component.Status);
}
```

---

## Test Organization

### Project Structure
```
tests/
├── Unit/
│   ├── Domain/
│   │   ├── ValueObjects/
│   │   └── Aggregates/
│   ├── Application/
│   │   └── Services/
│   └── Infrastructure/
│       └── Repositories/
├── Integration/
│   ├── Repositories/
│   └── Services/
└── TestUtilities/
    ├── Builders/
    ├── Fixtures/
    └── Mocks/
```

---

## Coverage Targets

| Layer | Target | Rationale |
|-------|--------|-----------|
| **Domain** (Value Objects, Aggregates) | 100% | Business logic must be fully tested |
| **Application** (Services, Handlers) | 90%+ | Complex workflows need coverage |
| **Infrastructure** (Repositories) | 70%+ | Integration tests cover persistence |
| **API** (Controllers, Endpoints) | 60%+ | Contract testing via integration tests |

---

## Anti-Patterns

| Pattern | Issue |
|---------|-------|
| Testing implementation details | Brittle tests, breaks on refactoring |
| No test data builders | Test setup becomes verbose and duplicated |
| Mocking domain objects | Tests don't validate domain logic |
| Async void tests | Hard to track failures |
| No CancellationToken in async tests | Doesn't test cancellation handling |
