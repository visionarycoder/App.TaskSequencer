---
metadata:
  version: "1.3.1"
  created: "2025-01-09"
  modified: "2025-01-09"
  type: "index"
  location: "docs/"
  target_audience: "AI Code Assistant & Developers"
  description: "Navigation guide for all copilot instruction files"

---

# Documentation Index

**📍 Location**: Documentation organized into three hierarchical sections in `docs/`

## 📚 Three Main Documentation Sections

### 🏢 Section 1: BUSINESS REQUIREMENTS (`docs/business/`)

**Purpose**: What we're building, financial ecosystem context, CSV specifications, Phase 2 Orleans + Desktop architecture

**Files**:
- [`business/01-architecture-requirements.md`](business/01-architecture-requirements.md) ⭐⭐⭐ **START HERE**
  - Solution design & development approach
  - Financial business context
  - Three CSV file specifications
  - Task identity & execution model
  - Orleans + Desktop architecture overview
  - Dependency rules & glossary
  
- [`business/02-execution-sequencing-pipeline.md`](business/02-execution-sequencing-pipeline.md) ⭐⭐⭐ **IF IMPLEMENTING PHASE 1 CORE**
  - Transformation pipeline (6 phases)
  - CSV manifest → ExecutionInstance
  - Dependency resolution algorithm (2-phase with deadline validation)
  - Orleans grain interface specifications
  - Execution plan concept with critical path
  - Open questions requiring business clarification

- [`business/03-orleans-aspire-architecture.md`](business/03-orleans-aspire-architecture.md) ⭐⭐⭐ **IF IMPLEMENTING PHASE 2**
  - Four Orleans grain types with state contracts
  - Single-process desktop model
  - Grain lifecycle and communication patterns
  - Iterative refinement algorithm (multi-round convergence)
  - Difference sequence tracking
  - Aspire integration for desktop app

- [`business/04-implementation-plan-phase-2.md`](business/04-implementation-plan-phase-2.md) ⭐⭐⭐ **FOR PHASE 2 PLANNING**
  - 16-week sprint breakdown
  - Detailed task breakdown per sprint
  - Performance targets and success criteria
  - Risk mitigation strategies
  - Testing scenarios and acceptance criteria

- [`business/05-technology-stack-web-gui.md`](business/05-technology-stack-web-gui.md) ⭐⭐⭐ **BEFORE IMPLEMENTATION**
  - Technology choices (Orleans 8.x, WinUI 3, .NET 8)
  - GUI design specifications (Dashboard, Timeline, Violations, Settings)
  - MVVM pattern with Community Toolkit
  - Desktop visualization options
  - Windows 11 integration
  - Deployment as .msix package
  - System requirements & performance targets

- [`PHASE_2_REQUIREMENTS_SUMMARY.md`](PHASE_2_REQUIREMENTS_SUMMARY.md) ⭐⭐ **QUICK REFERENCE**
  - Executive summary of Phase 2 decisions
  - Technology stack overview
  - Quick navigation to detailed docs

- [`business/readme.md`](business/readme.md) - Navigation & concept overview

---

### 💻 Section 2: CODING STANDARDS (`docs/standards/`)

**Purpose**: How to write code following project rules and patterns

**Files**:
- [`standards/01-coding-standards.md`](standards/01-coding-standards.md)
  - Naming conventions (PascalCase, camelCase, UPPER_SNAKE_CASE)
  - **Async requirements** (Async suffix, CancellationToken ct) — **MANDATORY**
  - C# 14+ language features by priority
  - Type declarations & member ordering
  - Formatting rules & blank lines

- [`standards/02-async-requirements.md`](standards/02-async-requirements.md) ⚠️ **CRITICAL**
  - Three MANDATORY async requirements
  - ConfigureAwait patterns
  - Cancellation token handling
  - Violation examples & fines

- [`standards/03-code-quality-architecture.md`](standards/03-code-quality-architecture.md)
  - **Primitive Obsession avoidance** (CRITICAL)
  - Code quality metrics (hard limits)
  - DDD project structure
  - Value objects & strong typing
  - SOLID principles
  - Dependency injection patterns

- [`standards/readme.md`](standards/readme.md) - Navigation with critical rules reference table

---

### 🎨 Section 3: DESIGN PATTERNS (`docs/patterns/`)

**Purpose**: How to apply patterns in specific scenarios

**Files**:
- [`patterns/01-ddd.md`](patterns/01-ddd.md)
  - Repository pattern
  - Specification pattern
  - Domain events
  - Aggregate root design

- [`patterns/02-validation.md`](patterns/02-validation.md)
  - Validation patterns
  - Error handling strategies
  - Result pattern vs exceptions
  - Guard clauses

- [`patterns/03-testing.md`](patterns/03-testing.md)
  - Unit testing approaches
  - Integration testing
  - Test data builders
  - Mock strategies

- [`patterns/readme.md`](patterns/readme.md) - Pattern selection guide & task-based navigation

---

## 📚 Supporting Documentation (Root Level)

These legacy files remain in `docs/` for reference:

- `readme.md` - Original comprehensive standards document (50 KB, 31 sections)
- `CHANGELOG.md` - Version history and changes
- `summary.md` - Project overview
- `completion_report.md` - Project completion summary
- `quick_start.md` - Contributor quick start guide
- `migration_summary.md` - Documentation migration notes
- `async_requirements.md` - Original async patterns file
- `patterns_ddd.md`, `patterns_validation.md`, `patterns_testing.md` - Original pattern guides

---

## 🚀 Quick Start by Task

### "I'm starting a new feature" (Developer)
1. **Business Context**: Read [`business/01-architecture-requirements.md`](business/01-architecture-requirements.md)
2. **Coding Standards**: Read [`standards/01-coding-standards.md`](standards/01-coding-standards.md)
3. **Patterns**: Pick from [`patterns/`](patterns/) based on feature type

### "I'm implementing Orleans execution sequencing" (Developer)
1. Read [`business/03-orleans-aspire-architecture.md`](business/03-orleans-aspire-architecture.md) (grain architecture)
2. Read [`business/02-execution-sequencing-pipeline.md`](business/02-execution-sequencing-pipeline.md) (execution algorithm)
3. Read [`business/01-architecture-requirements.md`](business/01-architecture-requirements.md) (CSV specs)
4. Consult [`standards/01-coding-standards.md`](standards/01-coding-standards.md) (naming)
5. Consult [`standards/02-async-requirements.md`](standards/02-async-requirements.md) (Orleans grains use async)
6. Consult [`patterns/01-ddd.md`](patterns/01-ddd.md) (grain design as aggregate roots)

### "I'm building the Phase 2 desktop GUI" (Developer)
1. Read [`business/04-implementation-plan-phase-2.md`](business/04-implementation-plan-phase-2.md) (Sprints 10-12 section)
2. Read [`business/05-technology-stack-web-gui.md`](business/05-technology-stack-web-gui.md) (GUI design)
3. Review [`PHASE_2_REQUIREMENTS_SUMMARY.md`](PHASE_2_REQUIREMENTS_SUMMARY.md) (tech stack overview)
4. Follow MVVM pattern from Community Toolkit documentation
5. Use WinUI 3 DataGrid for task lists
6. Implement file pickers with Windows.Storage API

### "I need Phase 2 planning overview" (Manager/Architect)
1. Read [`PHASE_2_REQUIREMENTS_SUMMARY.md`](PHASE_2_REQUIREMENTS_SUMMARY.md) (executive summary)
2. Review [`business/03-orleans-aspire-architecture.md`](business/03-orleans-aspire-architecture.md) (architecture)
3. Study [`business/04-implementation-plan-phase-2.md`](business/04-implementation-plan-phase-2.md) (sprint breakdown)
4. Check [`business/05-technology-stack-web-gui.md`](business/05-technology-stack-web-gui.md) (tech stack risks)

### "I'm writing async code" (Developer)
1. **CRITICAL**: Read [`standards/02-async-requirements.md`](standards/02-async-requirements.md) 
   - ❌ Async suffix MANDATORY
   - ❌ CancellationToken ct MANDATORY
   - ❌ Wrong parameter name is violation
2. Consult [`standards/03-code-quality-architecture.md`](standards/03-code-quality-architecture.md) (error handling)

### "I'm avoiding primitive obsession" (Developer)
1. Read [`standards/03-code-quality-architecture.md`](standards/03-code-quality-architecture.md) → **Primitive Obsession Section**
2. Learn value object pattern: `Identifier<T>` base class
3. Apply typed IDs: `ComponentId : Identifier<Guid>`
4. Create domain concepts as dedicated types

### "I'm implementing validation" (Developer)
1. Read [`patterns/02-validation.md`](patterns/02-validation.md) (patterns)
2. Consult [`standards/03-code-quality-architecture.md`](standards/03-code-quality-architecture.md) (value objects)
3. Consult [`standards/02-async-requirements.md`](standards/02-async-requirements.md) (if async validation)

### "I'm writing tests" (Developer)
1. Read [`patterns/03-testing.md`](patterns/03-testing.md)
2. Use Test Data Builders pattern
3. Reference sample Orleans grain tests (if needed)

### "I'm designing domain models" (Architect/Lead)
1. Read [`business/01-architecture-requirements.md`](business/01-architecture-requirements.md) (domain context)
2. Read [`business/02-execution-sequencing-pipeline.md`](business/02-execution-sequencing-pipeline.md) (transformation pipeline)
3. Consult [`patterns/01-ddd.md`](patterns/01-ddd.md) (DDD patterns)
4. Consult [`standards/03-code-quality-architecture.md`](standards/03-code-quality-architecture.md) (value objects & SOLID)

### "I'm optimizing performance" (Developer)
1. Reference original `readme.md` - Section 19 (Performance)
2. Reference original `readme.md` - Section 20 (.NET 10 Gotchas)

### "I'm reviewing code" (Code Reviewer)
1. Check against [`standards/03-code-quality-architecture.md`](standards/03-code-quality-architecture.md) metrics (30 line max, cyclomatic complexity ≤10)
2. Check async methods against [`standards/02-async-requirements.md`](standards/02-async-requirements.md)
3. Check naming against [`standards/01-coding-standards.md`](standards/01-coding-standards.md)
4. Check primitive obsession violations in [`standards/03-code-quality-architecture.md`](standards/03-code-quality-architecture.md)

---

## ⚠️ Critical Rules (Read First!)

All critical rules are documented in the standards section:

### 🔴 CRITICAL RULE #1: Async Method Naming & Parameters
**Location**: [`standards/02-async-requirements.md`](standards/02-async-requirements.md)

- ❌ ALL async methods MUST end with `Async`
- ❌ ALL async methods MUST have `CancellationToken ct` parameter (exact name)
- ❌ ALL CancellationToken parameters MUST be handled properly

Violations are **BREAKING**.

### 🔴 CRITICAL RULE #2: Primitive Obsession Avoidance
**Location**: [`standards/03-code-quality-architecture.md`](standards/03-code-quality-architecture.md)

- ❌ NO raw `Guid` for identities (use `Identifier<Guid>` subclass)
- ❌ NO raw `string` for domain concepts (emails, codes, IDs)
- ❌ NO raw collections of primitives (use typed collections)

**Impact**: Type system cannot enforce meaning; bugs in production

### 📌 CRITICAL RULE #3: No Underscore Prefixes
**Location**: [`standards/01-coding-standards.md`](standards/01-coding-standards.md)

- ❌ `private string _name;` — WRONG
- ✅ `private string name;` — CORRECT
- Modern C# doesn't require underscore prefixes

### 📌 CRITICAL RULE #4: Immutability by Default
**Location**: [`standards/03-code-quality-architecture.md`](standards/03-code-quality-architecture.md)

- ✅ Use `init`-only properties
- ✅ Use `record` types for value objects
- ✅ Use `readonly` for collections

### 📌 CRITICAL RULE #5: Code Quality Metrics
**Location**: [`standards/03-code-quality-architecture.md`](standards/03-code-quality-architecture.md)

- **Method Length**: 30 lines MAX
- **Cyclomatic Complexity**: 10 MAX per method
- **Constructor Parameters**: 5 MAX
- **Class Responsibilities**: 1 (Single Responsibility Principle)

---

## 📊 Current Documentation Organization (v1.3.1)

```
docs/
├── business/                    # 🏢 WHAT WE'RE BUILDING
│   ├── readme.md               # Navigation & concept overview
│   ├── 01-architecture-requirements.md    # ⭐ Start here (CSV specs, domain context)
│   └── 02-execution-sequencing-pipeline.md # Orleans implementation spec
│
├── standards/                   # 💻 HOW TO WRITE CODE
│   ├── readme.md               # Navigation with critical rules
│   ├── 01-coding-standards.md  # Naming, formatting, C# 14+ features
│   ├── 02-async-requirements.md # 🔴 MANDATORY: Async/cancellation rules
│   └── 03-code-quality-architecture.md # Metrics, DDD, SOLID, primitive obsession
│
├── patterns/                    # 🎨 DESIGN PATTERNS
│   ├── readme.md               # Pattern selection guide
│   ├── 01-ddd.md               # Repository, Specification, Domain Events
│   ├── 02-validation.md        # Validation strategies & error handling
│   └── 03-testing.md           # Unit & integration testing patterns
│
├── index.md                     # This file (navigation guide)
├── readme.md                    # Original 31-section comprehensive guide (legacy)
├── CHANGELOG.md                 # Version history
├── summary.md                   # Project overview
├── quick_start.md              # New contributor guide
└── migration_summary.md        # Documentation migration notes
```

---

### Directory Purpose Summary

| Folder | Purpose | Audience | Read When |
|--------|---------|----------|-----------|
| **business/** | What we're building | Architects, Business Analysts | Understanding domain, implementing features |
| **standards/** | How to write code | All Developers | Writing any code, code review |
| **patterns/** | Design patterns | Senior Developers, Architects | Complex scenarios, architecture decisions |

---

## � Cross-References

### By Topic

| Topic | Primary Location | Alternative Locations |
|-------|------------------|----------------------|
| **Async Methods** | `standards/02-async-requirements.md` | `standards/01-coding-standards.md` (naming) |
| **Primitive Obsession** | `standards/03-code-quality-architecture.md` | |
| **Value Objects** | `standards/03-code-quality-architecture.md` | `patterns/01-ddd.md` |
| **Validation** | `patterns/02-validation.md` | `standards/03-code-quality-architecture.md` (error handling) |
| **Testing** | `patterns/03-testing.md` | |
| **DDD Patterns** | `patterns/01-ddd.md` | `standards/03-code-quality-architecture.md` (structure) |
| **Naming Conventions** | `standards/01-coding-standards.md` | |
| **Code Quality Metrics** | `standards/03-code-quality-architecture.md` | |
| **Orleans Architecture** | `business/02-execution-sequencing-pipeline.md` | |
| **CSV Specifications** | `business/01-architecture-requirements.md` | `business/02-execution-sequencing-pipeline.md` (pipeline) |
| **Financial Domain** | `business/01-architecture-requirements.md` | |
| **SOLID Principles** | `standards/03-code-quality-architecture.md` | |
| **Dependency Injection** | `standards/03-code-quality-architecture.md` | |

### By Development Task

| Task | Read | Then Reference | Then Use |
|------|------|-----------------|----------|
| Start new feature | business/01 | standards/01 | patterns/* |
| Implement Orleans sequencing | business/02 | standards/02 | patterns/01 |
| Write async code | standards/02 | standards/01 | — |
| Design domain model | business/01 | patterns/01 | standards/03 |
| Add validation | patterns/02 | standards/03 | — |
| Write tests | patterns/03 | — | — |

---

## ❓ FAQ

**Q: Where do I start?**  
A: 
- Architect/Lead: Start with `business/01-architecture-requirements.md` → `business/02-execution-sequencing-pipeline.md`
- Developer: Start with `business/01-architecture-requirements.md` → your task-specific file
- New contributor: Read [`quick_start.md`](quick_start.md) → section-specific README files

**Q: Which file is authoritative?**  
A: The hierarchical sections (business/, standards/, patterns/) are authoritative v1.3.1+. Obsolete legacy files from v1.3.0 have been removed.

**Q: I found a reference to `async_requirements.md` - where is it?**  
A: That file was removed in v1.3.1. Use [`standards/02-async-requirements.md`](standards/02-async-requirements.md) instead. Update any code references to point to the new organized location.

**Q: What happened to the old pattern files?**  
A: `patterns_ddd.md`, `patterns_validation.md`, and `patterns_testing.md` were consolidated into the `/docs/patterns/` folder as `01-ddd.md`, `02-validation.md`, and `03-testing.md`. All content was preserved.

**Q: Are there different rules for different projects?**  
A: No. All rules in standards/ apply to all .NET code in the project.

**Q: What if I find a rule I disagree with?**  
A: All rules require consistency. Discuss with the team; update applies project-wide.

**Q: How often are these updated?**  
A: Check `CHANGELOG.md` for history. Quarterly review recommended as .NET evolves (new versions, new features).

**Q: What about the original `readme.md` (50 KB)?**  
A: It remains in `docs/` for reference but is superseded by the organized standards/ section. Prefer the organized files for new work.

**Q: Are these rules enforced by tooling?**  
A: Some (naming via C# conventions, async via compiler). Others (method length, naming) require code review discipline. Consider adding analyzers to `EditorConfig`.

**Q: Where do open questions go?**  
A: See `business/02-execution-sequencing-pipeline.md` - "17 Open Questions" section. Raised with business stakeholders for clarification.

---

## 🔄 Version & Maintenance

**Current Version**: v1.3.1 (2025-01-09)  
**Previous Version**: v1.3.0

### What Changed in v1.3.1
- ✅ Reorganized documentation into three logical sections (business, standards, patterns)
- ✅ Created comprehensive code quality guide covering primitive obsession
- ✅ Updated master index for new hierarchical structure
- ✅ Preserved all original content; improved discoverability
- ✅ Added section-specific README files with navigation

### Key Versions
- **v1.3.1**: Documentation reorganization (current) ← YOU ARE HERE
- **v1.3.0**: Expanded to 31 sections
- **v1.2.0**: Added async requirements
- **v1.1.0**: Expanded from 10 to 22 sections  
- **v1.0.0**: Initial release

See `CHANGELOG.md` for complete history.

---

## 💡 For Different Roles

### 🏗️ Architects / Technical Leads
1. Start: [`business/01-architecture-requirements.md`](business/01-architecture-requirements.md)
2. Deep dive: [`business/02-execution-sequencing-pipeline.md`](business/02-execution-sequencing-pipeline.md)
3. Reference: [`standards/03-code-quality-architecture.md`](standards/03-code-quality-architecture.md) (metrics & SOLID)
4. Reference: [`patterns/01-ddd.md`](patterns/01-ddd.md) (aggregate design)

### 👨‍💻 Feature Developers
1. Start: [`business/01-architecture-requirements.md`](business/01-architecture-requirements.md)
2. Read: [`standards/01-coding-standards.md`](standards/01-coding-standards.md)
3. If async: [`standards/02-async-requirements.md`](standards/02-async-requirements.md)
4. If validation: [`patterns/02-validation.md`](patterns/02-validation.md)
5. Reference: [`standards/03-code-quality-architecture.md`](standards/03-code-quality-architecture.md) (quality metrics)

### 🔍 Code Reviewers
Use this checklist:
- [ ] Business context: Does this align with [`business/`](business/)? 
- [ ] Naming: Does it follow [`standards/01-`](standards/01-coding-standards.md)?
- [ ] Async: Does it follow [`standards/02-`](standards/02-async-requirements.md)?
- [ ] Quality: ≤30 lines, ≤10 complexity, ≤5 params? (see [`standards/03-`](standards/03-code-quality-architecture.md))
- [ ] Pattern: Does it use appropriate patterns from [`patterns/`](patterns/)?
- [ ] Primitives: Any primitive obsession? (see [`standards/03-`](standards/03-code-quality-architecture.md))

### 🧪 Test Engineers / QA
1. Reference: [`patterns/03-testing.md`](patterns/03-testing.md)
2. Reference: [`standards/02-async-requirements.md`](standards/02-async-requirements.md) (for async testing)
3. Understand domain: [`business/01-architecture-requirements.md`](business/01-architecture-requirements.md)

### 📚 Documentation / Knowledge Management
1. All originals in `docs/` root
2. Organized versions in `docs/business/`, `docs/standards/`, `docs/patterns/`
3. Update CHANGELOG.md for any changes
4. Cross-reference from this index.md

---

**Last Updated**: 2025-01-09  
**Maintained By**: GitHub Copilot  
**Project**: Task Sequencer  
**Status**: Reorganized & Ready ✅
