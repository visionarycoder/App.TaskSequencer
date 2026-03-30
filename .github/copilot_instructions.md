---
metadata:
  version: "1.3.1"
  created: "2025-01-09"
  modified: "2025-03-28"
  project: "App.TaskSequencer"
  target_audience: "AI Code Assistant & Developers"
  priority: "critical"
  type: "index_redirect"
  location: "docs/instructions/"
  notice: "This is a redirect/navigation file. Full documentation is in docs/instructions/ and test data in docs/data/"

---

# ⚠️ REDIRECT: Copilot Instructions Moved to /docs/

**All copilot instruction documentation is organized in the `/docs/instructions/` directory for improved accessibility with AI agents and developers.**

## 📍 PRIMARY LOCATIONS

**Main Index**: [`docs/instructions/index.md`](../docs/instructions/index.md)  
**Getting Started**: [`docs/instructions/readme.md`](../docs/instructions/readme.md)  
**Test Data**: [`docs/data/`](../docs/data/)

---

## 🚀 Quick Links

### Start Here
- **Main Instructions** → [`docs/instructions/index.md`](../docs/instructions/index.md)
- **Navigation Guide** → [`docs/instructions/readme.md`](../docs/instructions/readme.md)
- **Architecture Overview** → [`docs/instructions/architecture/README.md`](../docs/instructions/architecture/README.md)

### Critical Rules (Must Know)
1. **Async Methods** - [`docs/instructions/standards/02-async-requirements.md`](../docs/instructions/standards/02-async-requirements.md)
2. **No Primitive Obsession** - [`docs/instructions/standards/01-coding-standards.md`](../docs/instructions/standards/01-coding-standards.md)
3. **Code Quality** - [`docs/instructions/standards/03-code-quality-architecture.md`](../docs/instructions/standards/03-code-quality-architecture.md)
4. **Validation Patterns** - [`docs/instructions/patterns/02-validation.md`](../docs/instructions/patterns/02-validation.md)
5. **Testing Patterns** - [`docs/instructions/patterns/03-testing.md`](../docs/instructions/patterns/03-testing.md)

### Pattern Deep-Dives
- **Domain-Driven Design** → [`docs/instructions/patterns/01-ddd.md`](../docs/instructions/patterns/01-ddd.md)
- **Validation & Error Handling** → [`docs/instructions/patterns/02-validation.md`](../docs/instructions/patterns/02-validation.md)
- **Testing Patterns** → [`docs/instructions/patterns/03-testing.md`](../docs/instructions/patterns/03-testing.md)

### Reference Documents
- **Complete Index** → [`docs/instructions/index.md`](../docs/instructions/index.md)
- **Business Requirements** → [`docs/instructions/business/README.md`](../docs/instructions/business/README.md)
- **Phase 2 Summary** → [`docs/instructions/PHASE_2_IMPLEMENTATION_SUMMARY.md`](../docs/instructions/PHASE_2_IMPLEMENTATION_SUMMARY.md)
- **Test Data** → [`docs/data/`](../docs/data/)

---

## ✨ Why Moved?

✅ **Better AI Discovery** - Claude, Cursor, and other AI assistants find content at standard `/docs/` location  
✅ **Industry Standard** - Follows GitHub and best practices conventions  
✅ **IDE Integration** - VS/VS Code naturally shows `/docs/` folder  
✅ **Future-Ready** - Prepared for GitHub Pages or documentation sites  
✅ **Cleaner Repo** - `.github/` for workflows, `/docs/` for documentation  

---

## 📊 Core Rules Summary

### 🔴 CRITICAL (Mandatory)
| Rule | Reference |
|------|-----------|
| Async methods must have `Async` suffix | [`docs/instructions/standards/02-async-requirements.md`](../docs/instructions/standards/02-async-requirements.md) |
| All async methods need `CancellationToken ct` parameter | [`docs/instructions/standards/02-async-requirements.md`](../docs/instructions/standards/02-async-requirements.md) |
| No raw primitives for domain concepts | [`docs/instructions/standards/01-coding-standards.md`](../docs/instructions/standards/01-coding-standards.md) |
| Classes must be sealed unless designed for inheritance | [`docs/instructions/standards/03-code-quality-architecture.md`](../docs/instructions/standards/03-code-quality-architecture.md) |
| Use value objects for all identity/business concepts | [`docs/instructions/patterns/01-ddd.md`](../docs/instructions/patterns/01-ddd.md) |
| Immutable by default (use `init`, records, `IReadOnly*`) | [`docs/instructions/standards/01-coding-standards.md`](../docs/instructions/standards/01-coding-standards.md) |

### 🟡 HIGH PRIORITY
| Rule | Reference |
|------|-----------|
| Use `is null` / `is not null` (never `== null`) | [`docs/instructions/standards/01-coding-standards.md`](../docs/instructions/standards/01-coding-standards.md) |
| No magic strings/numbers | [`docs/instructions/standards/01-coding-standards.md`](../docs/instructions/standards/01-coding-standards.md) |
| Guard clauses for validation | [`docs/instructions/patterns/02-validation.md`](../docs/instructions/patterns/02-validation.md) |

---

## 🎯 For Developers

```bash
# Read full documentation index
cat docs/instructions/index.md

# Browse by topic
cat docs/instructions/readme.md

# View specific areas
cat docs/instructions/architecture/README.md
cat docs/instructions/business/README.md

# Specific patterns
cat docs/instructions/patterns/01-ddd.md
cat docs/instructions/patterns/02-validation.md
cat docs/instructions/patterns/03-testing.md

# View test data
cat docs/data/task_definitions.csv
cat docs/data/execution_durations.csv
```

---

## 🤖 For AI Agents

All instruction files are now properly organized in `/docs/instructions/`:
- **Index**: `docs/instructions/index.md` - Navigation hub
- **Getting Started**: `docs/instructions/readme.md`
- **Architecture**: `docs/instructions/architecture/` - System design docs
- **Business**: `docs/instructions/business/` - Requirements & planning
- **Patterns**: `docs/instructions/patterns/` - Design patterns (DDD, Validation, Testing)
- **Standards**: `docs/instructions/standards/` - Coding standards, async requirements, quality

Test and reference data in `/docs/data/`:
- `task_definitions.csv` - Task manifest
- `execution_durations.csv` - Performance metrics
- `intake_events.csv` - Event samples

Use these files for:
- Code suggestion context
- Style and design validation
- Architecture review
- Async/patterns compliance
- Design pattern guidance

---

## 📋 Full File Inventory

```
docs/
├── copilot_instructions.md              (30 KB, v1.3.1) ⭐ PRIMARY
├── index.md                             (9 KB, v1.1.0)
├── quick_start.md                       (4 KB, v1.0.0)
├── summary_v1.3.0.md                    (10 KB, v1.3.0)
├── migration_summary.md                 (NEW)
├── completion_report.md                 (NEW)
├── async_requirements_v1.2.0.md         (5 KB, v1.2.0)
├── copilot_instructions_additions.md    (5 KB, v1.1.0)
├── patterns_ddd.md                      (8 KB, v1.0.0)
├── patterns_validation.md               (9 KB, v1.0.0)
└── patterns_testing.md                  (10 KB, v1.0.0)

.github/
└── copilot_instructions.md              (THIS FILE - redirect)
```

---

## ❓ FAQ

**Q: Where's the full instruction file?**  
A: → [`docs/copilot_instructions.md`](../docs/copilot_instructions.md) (31 sections)

**Q: What's the quick start?**  
A: → [`docs/quick_start.md`](../docs/quick_start.md) (5 minutes)

**Q: How do I navigate?**  
A: → [`docs/index.md`](../docs/index.md) (complete index with cross-references)

**Q: What patterns should I know?**  
A: → [`docs/patterns_ddd.md`](../docs/patterns_ddd.md), [`docs/patterns_validation.md`](../docs/patterns_validation.md), [`docs/patterns_testing.md`](../docs/patterns_testing.md)

**Q: What changed?**  
A: → [`docs/migration_summary.md`](../docs/migration_summary.md)

---

## 🔗 Direct Links to Key Sections

### Naming Conventions
- [`docs/copilot_instructions.md` §2.1](../docs/copilot_instructions.md#21-naming-conventions) - All naming rules

### Technology Stack
- [`docs/copilot_instructions.md` §1](../docs/copilot_instructions.md#1-technology-definitions) - .NET 10, C# 14+, Null Safety

### Async Pattern (CRITICAL)
- [`docs/copilot_instructions.md` §11](../docs/copilot_instructions.md#11-concurrency--parallelism) - Complete async requirements
- [`docs/async_requirements_v1.2.0.md`](../docs/async_requirements_v1.2.0.md) - Detailed async examples

### Domain Design
- [`docs/copilot_instructions.md` §4](../docs/copilot_instructions.md#4-design-patterns--architecture) - DDD, value objects
- [`docs/patterns_ddd.md`](../docs/patterns_ddd.md) - Deep-dive: Repository, Specification, Domain Events

### Validation & Errors
- [`docs/copilot_instructions.md` §12](../docs/copilot_instructions.md#12-exception-handling-strategy) - Exception handling
- [`docs/patterns_validation.md`](../docs/patterns_validation.md) - Deep-dive: Validation, error handling

### Testing
- [`docs/copilot_instructions.md` §6](../docs/copilot_instructions.md#6-testing-requirements) - Test classifications
- [`docs/patterns_testing.md`](../docs/patterns_testing.md) - Deep-dive: Unit & integration testing

---

## 📌 Version Information

| Component | Version | Status |
|-----------|---------|--------|
| copilot_instructions.md | 1.3.1 | ✅ Current (docs/) |
| index.md | 1.1.0 | ✅ Current (docs/) |
| patterns_ddd.md | 1.0.0 | ✅ Current (docs/) |
| patterns_validation.md | 1.0.0 | ✅ Current (docs/) |
| patterns_testing.md | 1.0.0 | ✅ Current (docs/) |

**This redirect**: v1.0.0 (2025-01-09)

---

## 🎯 Next Steps

1. **Open Main File**: [`docs/copilot_instructions.md`](../docs/copilot_instructions.md)
2. **Browse Topics**: [`docs/index.md`](../docs/index.md)
3. **Learn Quick Rules**: [`docs/quick_start.md`](../docs/quick_start.md)
4. **Deep-Dive Patterns**: `docs/patterns_*.md`

---

**Last Updated**: 2025-01-09  
**Location**: All content in `/docs/` directory  
**This File**: Backward-compatible redirect (`.github/copilot_instructions.md`)  
**Status**: ✅ Ready to use
