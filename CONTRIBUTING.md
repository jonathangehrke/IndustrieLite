# Contributing to IndustrieLite

Thank you for your interest in contributing! This document provides guidelines for contributing to this project.

## How to Contribute

### Reporting Bugs

Use the [Bug Report](https://github.com/jonathangehrke/IndustrieLite-dev/issues/new?template=bug_report.yml) template to report bugs.

**Before submitting:**
- Search existing issues to avoid duplicates
- Provide clear steps to reproduce
- Include relevant logs, screenshots, or error messages

### Suggesting Features

Use the [Feature Request](https://github.com/jonathangehrke/IndustrieLite-dev/issues/new?template=feature_request.yml) template.

**Good feature requests include:**
- Clear use case and motivation
- Consideration of alternatives
- Awareness of potential trade-offs

### Pull Requests

1. **Fork & Branch**: Create a feature branch from `main`
2. **Code**: Follow the coding standards below
3. **Test**: Ensure `dotnet test` passes and add tests for new features
4. **Commit**: Use [Conventional Commits](https://www.conventionalcommits.org/) format
5. **PR**: Open a Pull Request with a clear description

## Development Setup

### Prerequisites
- **Godot 4.x** (Mono/.NET version) - [Download](https://godotengine.org/download)
- **.NET SDK 8.0+** - [Download](https://dotnet.microsoft.com/download)

### Local Setup
```bash
git clone https://github.com/jonathangehrke/IndustrieLite-dev.git
cd IndustrieLite-dev
dotnet restore
```

Open `project.godot` in Godot 4 and press **F5** to run.

## Coding Standards

### Code Style
- **C# Identifiers**: English (classes, methods, properties, fields)
- **Comments/Logs**: German (bilingual codebase)
- **Formatting**: Run `dev-scripts/format.ps1 -Verify` before committing
- **No Magic Strings**: Use constants from `code/runtime/core/Ids.cs`

### Architecture Principles
- **Dependency Injection**: Use `DIContainer` as Composition Root (see `docs/DI-POLICY.md`)
- **Event-Driven**: Use `EventHub` for decoupling UI from game logic
- **Deterministic Simulation**: State changes via fixed-timestep `ITickable.Tick()`
- **Result Pattern**: Use `Result<T>` for structured error handling

### Quality Gates
All PRs must pass:
- ✅ `dotnet format --verify-no-changes` (code formatting)
- ✅ `dotnet build -warnaserror` (no warnings)
- ✅ `dotnet test` (all tests passing)
- ✅ CI checks (see `.github/workflows/`)

## Commit Message Format

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>: <description>

[optional body]
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `refactor`: Code restructuring without behavior change
- `docs`: Documentation changes
- `test`: Test additions or fixes
- `chore`: Build process, tooling, dependencies

**Examples:**
```
feat: add building upgrade system
fix: resolve pathfinding crash on empty road grid
refactor: migrate ProductionManager to GameClock ticks
docs: update architecture diagram with EventHub flow
```

## Documentation

When adding features:
- Update relevant docs in `docs/`
- Add XML documentation for public APIs
- Update `ARCHITECTURE.md` for architectural changes

## Questions?

- Open a [Discussion](https://github.com/jonathangehrke/IndustrieLite-dev/discussions)
- Check existing [Issues](https://github.com/jonathangehrke/IndustrieLite-dev/issues)

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
