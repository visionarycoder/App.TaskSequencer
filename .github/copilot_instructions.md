---
metadata:
  version: "1.3.1"
  created: "2025-01-09"
  modified: "2025-01-09"
  project: "Dependencies"
  target_audience: "AI Code Assistant & Developers"
  priority: "critical"
  type: "index_redirect"
  location: "docs/"
  notice: "This is a redirect file. Full content moved to /docs/ for better AI accessibility."

---

# ⚠️ REDIRECT: Copilot Instructions Moved to /docs/

**All copilot instruction documentation has been migrated to the `/docs/` directory for improved accessibility with AI agents.**

## 📍 NEW LOCATION

**Primary File**: [`docs/copilot_instructions.md`](../docs/copilot_instructions.md)

---

## 🚀 Quick Links

### Start Here
- **Main Instructions** → [`docs/copilot_instructions.md`](../docs/copilot_instructions.md)
- **Navigation Guide** → [`docs/index.md`](../docs/index.md)
- **5-Minute Quick Start** → [`docs/quick_start.md`](../docs/quick_start.md)

### Critical Rules (Must Know)
1. **Async Methods** - [`docs/copilot_instructions.md` §11](../docs/copilot_instructions.md#111-async-method-naming--cancellation-token-requirements-mandatory)
2. **No Primitive Obsession** - [`docs/copilot_instructions.md` §4.3](../docs/copilot_instructions.md#43-primitive-obsession-avoidance-critical-rule)
3. **Immutability Required** - [`docs/copilot_instructions.md` §24](../docs/copilot_instructions.md)
4. **Sealed Classes** - [`docs/copilot_instructions.md` §25](../docs/copilot_instructions.md)
5. **Strong Typing** - [`docs/copilot_instructions.md` §28](../docs/copilot_instructions.md)

### Pattern Deep-Dives
- **Domain-Driven Design** → [`docs/patterns_ddd.md`](../docs/patterns_ddd.md)
- **Validation & Error Handling** → [`docs/patterns_validation.md`](../docs/patterns_validation.md)
- **Testing Patterns** → [`docs/patterns_testing.md`](../docs/patterns_testing.md)

### Reference Documents
- **Complete Index** → [`docs/index.md`](../docs/index.md)
- **Migration Summary** → [`docs/migration_summary.md`](../docs/migration_summary.md)
- **Completion Report** → [`docs/completion_report.md`](../docs/completion_report.md)
- **Async Requirements** → [`docs/async_requirements_v1.2.0.md`](../docs/async_requirements_v1.2.0.md)

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
| Async methods must have `Async` suffix | [`docs/copilot_instructions.md` §11.1](../docs/copilot_instructions.md#111-async-method-naming--cancellation-token-requirements-mandatory) |
| All async methods need `CancellationToken ct` parameter | [`docs/copilot_instructions.md` §11.1](../docs/copilot_instructions.md#111-async-method-naming--cancellation-token-requirements-mandatory) |
| No raw primitives for domain concepts | [`docs/copilot_instructions.md` §4.3](../docs/copilot_instructions.md#43-primitive-obsession-avoidance-critical-rule) |
| Classes must be sealed unless designed for inheritance | [`docs/copilot_instructions.md` §25](../docs/copilot_instructions.md) |
| Use value objects for all identity/business concepts | [`docs/copilot_instructions.md` §4.3](../docs/copilot_instructions.md#43-primitive-obsession-avoidance-critical-rule) |
| Immutable by default (use `init`, records, `IReadOnly*`) | [`docs/copilot_instructions.md` §24](../docs/copilot_instructions.md) |

### 🟡 HIGH PRIORITY
| Rule | Reference |
|------|-----------|
| Use `is null` / `is not null` (never `== null`) | [`docs/copilot_instructions.md` §3.1](../docs/copilot_instructions.md#31-mandatory-standards) |
| No magic strings/numbers | [`docs/copilot_instructions.md` §3.1](../docs/copilot_instructions.md#31-mandatory-standards) |
| Guard clauses for validation | [`docs/copilot_instructions.md` §10.2](../docs/copilot_instructions.md#102-guard-clauses) |

---

## 🎯 For Developers

```bash
# Read full instructions
cat docs/copilot_instructions.md

# Browse by topic
cat docs/index.md

# 5-minute start
cat docs/quick_start.md

# Specific patterns
cat docs/patterns_ddd.md
cat docs/patterns_validation.md
cat docs/patterns_testing.md
```

---

## 🤖 For AI Agents

All instruction files are now in `/docs/` directory:
- Primary: `docs/copilot_instructions.md` (v1.3.1)
- Navigation: `docs/index.md`
- Patterns: `docs/patterns_*.md`

Use these files for:
- Code suggestion context
- Style validation
- Architecture review
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
