# Technical Patterns & Best Practices

Domain-Driven Design patterns, validation strategies, and testing approaches.

---

## 📄 Documents in This Section

### 1. [Domain-Driven Design (DDD) Patterns](01-ddd.md)
**Read First**: ✅ YES (if implementing domain models)  
**Purpose**: Building rich domain models using DDD principles

**Contains**:
- DDD project structure
- Repository pattern implementation
- Specification pattern for complex queries
- Domain events for state changes
- Aggregate root design
- Value object patterns

**Key Concepts**:
- Repository abstraction
- Aggregate boundaries
- Domain events and event sourcing
- Bounded contexts

**When to Read**:
- Building domain models
- Designing repository abstractions
- Handling complex business rules
- Implementing aggregate patterns

---

### 2. [Validation & Error Handling Patterns](02-validation.md)
**Read First**: ✅ YES (if implementing validation)  
**Purpose**: Consistent validation and error handling strategies

**Contains**:
- Validation patterns
- Result pattern vs exceptions
- Guard clauses
- Fluent validation chains
- Specification pattern for reusable validations
- Error handling strategies

**Key Concepts**:
- Guard clauses for early validation
- Result pattern (Success/Failure)
- Custom exceptions with context
- Validation composition

**When to Read**:
- Implementing validation logic
- Error handling strategy
- Input validation
- Business rule enforcement

---

### 3. [Testing Strategies & Patterns](03-testing.md)
**Read First**: ✅ YES (when writing tests)  
**Purpose**: Unit and integration testing approaches

**Contains**:
- Unit testing strategies
- Integration testing
- Test organization
- Test data builders
- Mocking strategies
- AAA pattern (Arrange, Act, Assert)

**Key Concepts**:
- Test classification (Unit, Integration, Domain, API)
- Test naming conventions
- Test data builders
- Fixture management

**When to Read**:
- Writing unit tests
- Setting up test projects
- Testing domain logic
- Integration test strategy

---

## 🎯 Quick Navigation by Task

| Task | Document |
|------|----------|
| "How do I structure domain models?" | 01-ddd.md |
| "When should I use Repository?" | 01-ddd.md (Repository Pattern) |
| "How do I implement validation?" | 02-validation.md |
| "What's the Result pattern?" | 02-validation.md (Result Pattern vs Exceptions) |
| "How do I write tests?" | 03-testing.md |
| "What's a test builder?" | 03-testing.md (Test Data Builders) |
| "How do I mock dependencies?" | 03-testing.md (Mocking Strategies) |

---

## 📊 Pattern Selection Guide

### Use Case → Pattern

| When You're... | Use This Pattern | In This Document |
|---|---|---|
| Building a domain model | Domain-Driven Design | 01-ddd.md |
| Querying data with complex filters | Specification | 01-ddd.md |
| Publishing state changes | Domain Events | 01-ddd.md |
| Abstracting data access | Repository | 01-ddd.md |
| Validating input | Validation (Guard/Result) | 02-validation.md |
| Handling operation success/failure | Result Pattern | 02-validation.md |
| Writing unit tests | AAA Pattern + Test Builders | 03-testing.md |
| Testing data access | Integration Testing | 03-testing.md |
| Reusing test setup | Fixtures & Builders | 03-testing.md |

---

## 🔗 Related Documentation

- [Coding Standards](../standards/01-coding-standards.md) - How to format code
- [Async Requirements](../standards/02-async-requirements.md) - Async patterns
- [Code Quality](../standards/03-code-quality-architecture.md) - Quality metrics
