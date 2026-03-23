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

**📍 Location**: All files are in the `docs/` directory (organized for AI agents and developers)

## 🎯 Core Documentation Files

### 1. Main Instructions
**File**: `README.md` (v1.3.1, ~50 KB)  
**Purpose**: Comprehensive rules, standards, and requirements  
**Sections**: 31  
**Read First**: ⭐⭐⭐ YES

**Quick Navigation**:
- **§1-2**: Technology & coding standards
- **§3**: SOLID principles & DDD architecture
- **§4**: Design patterns & primitive obsession avoidance (CRITICAL)
- **§5-7**: Code formatting, testing, logging
- **§8-10**: Configuration, anti-patterns, idioms
- **§11-20**: Concurrency, exceptions, records, resources, fluent APIs, extensions
- **§21-31**: Documentation, feature organization, performance, .NET 10 gotchas

---

### 2. Pattern-Specific Guides
Choose based on your current task:

#### **patterns_ddd.md** (Domain-Driven Design)
- Repository pattern implementation
- Specification pattern
- Domain events
- Aggregate root design
- **Use When**: Building domain models or business logic

#### **patterns_validation.md** (Validation & Error Handling)
- Validation patterns
- Error handling strategies
- Result pattern vs exceptions
- Guard clauses
- **Use When**: Implementing validation or exception handling

#### **patterns_testing.md** (Testing Strategies)
- Unit testing approaches
- Integration testing
- Test data builders
- Mock strategies
- **Use When**: Writing tests or designing testable code

#### **async_requirements.md** (Async/Await Details)
- Async method naming (MANDATORY: `*Async` suffix)
- CancellationToken requirements (MANDATORY: parameter `ct`)
- ConfigureAwait patterns
- Cancellation handling
- **Use When**: Writing async code or fixing async issues

---

## 📚 Supporting Documentation

### Changelog
**File**: `CHANGELOG.md`  
**Purpose**: Version history, improvements, breaking changes  
**Latest**: v1.3.1  
**Use When**: Understanding what changed between versions

### Project Reports
**File**: `completion_report.md`  
**Purpose**: Project completion summary  

**File**: `summary.md`  
**Purpose**: High-level project overview  

**File**: `quick_start.md`  
**Purpose**: Quick start guide for new contributors  

**File**: `migration_summary.md`  
**Purpose**: Documentation of code migrations and consolidations  

---

## 🚀 Quick Start by Task

### "I'm starting a new feature"
1. Read: `README.md` - Sections 1-4 (Technology, Coding Standards, Design Patterns)
2. Reference: `patterns_ddd.md` (if domain-focused)
3. Consult: `README.md` - Section 4.3 (Primitive Obsession)

### "I'm writing async code"
1. Read: `async_requirements.md` (CRITICAL - mandatory requirements)
2. Reference: `README.md` - Section 11 (Concurrency)
3. Confirm: All methods end with `Async` and have `CancellationToken ct` parameter

### "I'm adding validation"
1. Read: `patterns_validation.md` (complete validation guide)
2. Reference: `README.md` - Section 12 (Exception Handling)
3. Reference: `README.md` - Section 4.3 (Value Objects)

### "I'm writing tests"
1. Read: `patterns_testing.md` (testing patterns)
2. Reference: `README.md` - Section 6 (Testing Requirements)
3. Use: Test Data Builders pattern from patterns_testing.md

### "I need to refactor code"
1. Read: `README.md` - Section 4 (Design Patterns)
2. Reference: `README.md` - Section 9 (Anti-patterns)
3. Check: SOLID principles table in Section 4.4

### "I'm optimizing performance"
1. Read: `README.md` - Section 19 (Performance & Allocation Patterns)
2. Reference: `README.md` - Section 20 (.NET 10 Specific Gotchas)
3. Measure: Use dotTrace or similar profiler

### "I'm documenting code"
1. Read: `README.md` - Section 17 (Documentation Standards)
2. Reference: XML documentation examples
3. Remember: "Why" over "What" principle

---

## ⚠️ Critical Rules (Read First!)

These rules are marked **CRITICAL** 🔴 in `README.md`:

1. **Async Method Naming** (`README.md` §11)
   - ALL async methods MUST end with `Async`
   - ALL async methods MUST have `CancellationToken ct` parameter
   - ALL CancellationToken parameters MUST be handled

2. **Primitive Obsession Avoidance** (`README.md` §4.3)
   - NO raw `Guid` for identities (use `Identifier<Guid>`)
   - NO raw `string` for domain concepts (use value objects)
   - NO raw collections of primitives (use typed collections)

3. **No Underscore Prefixes** (`README.md` §2.1)
   - Modern C# doesn't require `_field` prefixes
   - Use `private string name;` not `private string _name;`

4. **Immutability by Default** (`README.md` §24)
   - Use `init`-only properties
   - Use `record` types for value objects
   - Use `readonly` for collections

5. **Sealed Classes** (`README.md` §25)
   - Seal classes unless explicitly designed for inheritance
   - Prevents accidental inheritance chains

---

## 📊 File Organization

```
docs/
├── README.md                    # ⭐ Start here (main instructions, 31 sections)
├── CHANGELOG.md                 # Version history & changes
├── index.md                      # This file (navigation guide)
├── patterns_ddd.md              # Domain-Driven Design patterns
├── patterns_validation.md       # Validation & error handling
├── patterns_testing.md          # Testing patterns & strategies
├── async_requirements.md        # Async/await mandatory rules
├── quick_start.md               # New contributor quick start
├── completion_report.md         # Project completion summary
├── summary.md                   # Project overview
└── migration_summary.md         # Migration documentation
```

---

## 🔄 Version Information

**Current Version**: v1.3.1 (2025-01-09)

### Key Versions
- **v1.3.1**: Documentation reorganization (current)
- **v1.3.0**: Expanded to 31 sections
- **v1.2.0**: Added async requirements
- **v1.1.0**: Expanded from 10 to 22 sections
- **v1.0.0**: Initial release

See `CHANGELOG.md` for complete version history.

---

## 💡 How to Use This Documentation

### For AI Code Assistants
1. Always start with `README.md` (sections 1-4)
2. Consult pattern files for specific domains
3. Cross-reference anti-patterns (Section 9)
4. Check async_requirements.md for any Task/async code

### For Developers
1. Skim `README.md` - Table of Contents
2. Jump to relevant section based on current task
3. Review code examples (✅ GOOD / ❌ AVOID patterns)
4. Consult pattern-specific files for deep dives

### For Code Reviews
1. Check against anti-patterns (Section 9)
2. Verify naming conventions (Section 2.1)
3. Ensure async methods follow requirements (Section 11)
4. Validate design pattern usage

---

## 🔗 Cross-References

| Topic | Primary File | Alternative |
|-------|-------------|-------------|
| Async Requirements | `async_requirements.md` | `README.md` §11 |
| Validation | `patterns_validation.md` | `README.md` §12 |
| Testing | `patterns_testing.md` | `README.md` §6 |
| DDD Patterns | `patterns_ddd.md` | `README.md` §4 |
| Naming | `README.md` §2.1 | Any file |
| Exception Handling | `README.md` §12 | `patterns_validation.md` |
| Performance | `README.md` §19 | `README.md` §20 |
| Immutability | `README.md` §24 | `patterns_ddd.md` |
| Documentation | `README.md` §17 | `quick_start.md` |

---

## ❓ FAQ

**Q: Which file should I read first?**  
A: Always start with `README.md` - it contains all core rules and standards.

**Q: Is async_requirements.md separate or in README.md?**  
A: Both - `async_requirements.md` provides detailed async patterns, while `README.md` §11 covers the mandatory rules.

**Q: What if files seem to contradict each other?**  
A: `README.md` is authoritative. Pattern files provide focused guidance on specific domains.

**Q: How often are these updated?**  
A: Check `CHANGELOG.md` for update history. Quarterly reviews recommended for new .NET versions.

**Q: Where do I report documentation issues?**  
A: Create an issue in the repository with the file name and section number.

---

**Last Updated**: 2025-01-09  
**Maintained By**: GitHub Copilot  
**Project**: Dependencies  
**Status**: Complete & Organized ✅
