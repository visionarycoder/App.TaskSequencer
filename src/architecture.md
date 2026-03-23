# volatility-based decomposition architecture

## Overview

This project follows volatility-based decomposition, organizing code by how frequently it changes. The architecture is divided into four component types, each isolated in separate "vaults" with clear boundaries and dependencies.

## Architecture Layers

### 1. Client Layer (`Client.Core.*`)
**Volatility: HIGH** - User interfaces and presentation logic change frequently

- **Contract**: `Client.Core.Contract` - Defines UI service interfaces
- **Service**: `Client.Core.Service` - Concrete implementations (Console, Web, etc.)

**Responsibilities**:
- User interaction and display
- Input validation and formatting
- Presentation logic

**Example**: `ConsoleClientService` handles console-based UI operations

### 2. Manager Layer (`Manager.Orchestration.*`)
**Volatility: MEDIUM** - Orchestration workflows change as business processes evolve

- **Contract**: `Manager.Orchestration.Contract` - Defines orchestration interfaces
- **Service**: `Manager.Orchestration.Service` - Orchestration implementations

**Responsibilities**:
- Workflow coordination
- Service composition
- Call sequencing and error handling

**Dependencies**: Depends on Engine and Access layers

**Example**: `WorkflowOrchestrationService` coordinates task execution flows

### 3. Engine Layer (`Engine.Sequencing.*`)
**Volatility: LOW** - Business algorithms and rules are stable once defined

- **Contract**: `Engine.Sequencing.Contract` - Defines business rule interfaces and domain models
- **Service**: `Engine.Sequencing.Service` - Algorithm implementations

**Responsibilities**:
- Core business logic and algorithms
- Domain model definitions
- Business rule validation

**No Dependencies**: Should not depend on higher layers

**Example**: `ExecutionSequencingEngine` implements task sequencing algorithms

### 4. Access Layer (`Access.DataModel.*`)
**Volatility: LOW-MEDIUM** - Data structures are stable; access patterns may evolve

- **Contract**: `Access.DataModel.Contract` - Defines data models and access interfaces
- **Service**: `Access.DataModel.Service` - Data access implementations

**Responsibilities**:
- Data persistence and retrieval
- Data validation
- Resource abstraction

**No Dependencies**: Should not depend on higher layers

**Example**: `CsvDataAccessService` handles CSV file loading and result persistence

## Directory Structure

```
src/
├── Client.Core.Contract/
│   └── Client.Core.Contract.csproj
├── Client.Core.Service/
│   ├── Client.Core.Service.csproj
│   └── ConsoleClientService.cs
├── Manager.Orchestration.Contract/
│   └── Manager.Orchestration.Contract.csproj
├── Manager.Orchestration.Service/
│   ├── Manager.Orchestration.Service.csproj
│   └── WorkflowOrchestrationService.cs
├── Engine.Sequencing.Contract/
│   ├── Engine.Sequencing.Contract.csproj
│   ├── ISequencingEngine.cs
│   └── TaskDefinition.cs
├── Engine.Sequencing.Service/
│   ├── Engine.Sequencing.Service.csproj
│   └── ExecutionSequencingEngine.cs
├── Access.DataModel.Contract/
│   ├── Access.DataModel.Contract.csproj
│   ├── IDataAccessService.cs
│   └── Models.cs
├── Access.DataModel.Service/
│   ├── Access.DataModel.Service.csproj
│   └── CsvDataAccessService.cs
└── ConsoleApp/
    ├── ConsoleApp.csproj
    └── Program.cs
```

## Dependency Flow

```
Client.Core.Service
    ↓
Manager.Orchestration.Service
    ├→ Engine.Sequencing.Service
    └→ Access.DataModel.Service
```

**Key Principle**: Dependencies flow inward (downward). Higher layers depend on lower layers; lower layers never depend on higher layers.

## Vault Structure

Each vault follows a consistent pattern:

```
<ComponentType>.<Vault>.Contract/
├── Interfaces (I*)
└── Domain Models/DTOs

<ComponentType>.<Vault>.Service/
├── Implementations
└── Supporting Types
```

### Naming Conventions

- **Contracts** define: Interfaces, domain models, records, enums
- **Services** implement: Business logic, algorithms, data operations
- **All async methods**: Use `Async` suffix with `CancellationToken` as last parameter

## Adding New Features

1. **UI Changes**: Modify `Client.Core.Service`
2. **Business Logic**: Modify `Engine.Sequencing.Service`
3. **Data Access**: Modify `Access.DataModel.Service`
4. **Workflows**: Modify `Manager.Orchestration.Service`
5. **API Changes**: Create new vault or extend contracts

## Benefits

- **Stability**: Low-volatility code is isolated and rarely changes
- **Testability**: Each layer can be tested independently
- **Maintainability**: Clear responsibilities and boundaries
- **Scalability**: New implementations can be added without modifying existing code
- **Flexibility**: Multiple implementations of same contract can coexist

## Testing

- Test `Engine` layer algorithms independently (most important)
- Mock `Access` layer for manager orchestration tests
- Mock `Manager` layer for client UI tests
