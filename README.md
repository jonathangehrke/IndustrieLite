# IndustrieLite
> Economic simulation game built with Godot 4 and C#

[![Build & Tests](https://github.com/jonathangehrke/IndustrieLite-dev/actions/workflows/dotnet.yml/badge.svg)](https://github.com/jonathangehrke/IndustrieLite-dev/actions/workflows/dotnet.yml)
[![DI Pattern Enforcement](https://github.com/jonathangehrke/IndustrieLite-dev/actions/workflows/di-pattern-check.yml/badge.svg)](https://github.com/jonathangehrke/IndustrieLite-dev/actions/workflows/di-pattern-check.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## What is this?

Build and manage a small economy: place production buildings (farms, power plants, water pumps), connect them with roads, and fulfill dynamic city orders through a logistics system.

**Core Gameplay:**
- 🏗️ **Build** - Place farms, power plants, and infrastructure
- ⚡ **Manage Resources** - Balance power, water, and production
- 🚚 **Logistics** - Road-based transport system with pathfinding
- 💰 **Trade** - Fulfill dynamic market orders from cities
- 📈 **Upgrade** - Improve building capacity and truck logistics

## Tech Stack

- **Engine:** Godot 4.x (C# / .NET 8)
- **Architecture:** Clean DI, Event-Driven, Manager Pattern
- **Key Patterns:** Dependency Injection Container, CQRS, Deterministic Simulation (20Hz)
- **Data-Driven:** Buildings, Resources, and Recipes via `.tres` definitions
- **Testing:** Unit tests (.NET), Integration tests (Godot Headless), CI/CD (GitHub Actions)

## Key Features

### Architecture & Code Quality
✅ **Dependency Injection** - DIContainer as Composition Root, no Service Locator
✅ **Event-Driven Architecture** - EventHub decouples UI from game logic
✅ **Deterministic Simulation** - Fixed 20Hz timestep for reproducible state (save/load, future multiplayer)
✅ **Result Pattern** - Structured error handling with typed results
✅ **Data-Driven Design** - Game content via Godot Resources (.tres files)

### Gameplay Systems
✅ **Production Chains** - Resource flow from producers to consumers
✅ **Road Network** - A* pathfinding with dynamic rerouting
✅ **Market Simulation** - Cities generate orders with dynamic pricing
✅ **Building Upgrades** - Logistics capacity and speed improvements
✅ **Save/Load System** - Versioned saves with automatic migration

## Quick Start

### Prerequisites
- **Godot 4.x** (Mono/.NET version) - [Download](https://godotengine.org/download)
- **.NET SDK 8.0+** - [Download](https://dotnet.microsoft.com/download)

### Run the Game
1. Clone the repository
2. Open `project.godot` in Godot 4
3. Press **F5** to run

### Run Tests
```bash
dotnet test
```

## Architecture

This project demonstrates **Clean Architecture** principles in a game context:

- **DIContainer** - Explicit dependency injection, all manager dependencies wired at startup
- **ServiceContainer** - Named service registry for GDScript ↔ C# bridge (no typed Service Locator)
- **EventHub** - Pub/sub pattern for loose coupling between systems
- **Deterministic Simulation** - All state changes via fixed-timestep `ITickable.Tick()`, guarded by validation
- **Manager Pattern** - Clear separation: BuildingManager, EconomyManager, TransportManager, etc.

See [ARCHITECTURE.md](ARCHITECTURE.md) for:
- System overview with Mermaid diagrams
- ADR-style architecture decisions
- Known trade-offs & technical debt
- Test strategy

## Development

### Code Standards
- English identifiers (classes, methods, properties)
- German comments/logs (bilingual codebase)
- Centralized constants (no magic strings)
- XML documentation for public APIs

### Build & Quality Gates
```bash
# Format check
dotnet format --verify-no-changes

# Build
dotnet build

# Run tests
dotnet test
```

CI enforces:
- ✅ Code formatting (via `dotnet format`)
- ✅ Nullability checks (`<Nullable>enable</Nullable>`)
- ✅ Warnings as errors (`-warnaserror`)
- ✅ Roslyn analyzers

See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for detailed development docs:
- DI guidelines & migration
- GameClock design & phases
- UI architecture & signals
- Build commands & clean scripts

## Project Structure

```
.
├── .github/                # GitHub workflows & templates
├── assets/                 # Game assets (buildings, tiles, resources, etc.)
├── code/                   # C# source
│   ├── buildings/          # Building logic & services
│   ├── common/             # Shared utilities & constants
│   ├── data/               # Data models
│   ├── input/              # Input handling
│   ├── managers/           # Core managers (Building, Economy, Transport, etc.)
│   ├── roads/              # Road system
│   ├── runtime/            # Runtime services (GameManager, UIService, Database)
│   ├── sim/                # Simulation (ProductionSystem, ITickable)
│   └── transport/          # Transport subsystem
├── data/                   # Game data (.tres definitions)
├── dev-scripts/            # Development scripts (PowerShell)
├── docs/                   # Documentation
├── export/                 # Export presets
├── scenes/                 # Godot scenes
├── tests/                  # Unit & integration tests
├── tools/                  # CI tools & scripts
└── ui/                     # GDScript UI
```

## License

**MIT License** - Copyright (c) 2025 Jonathan Gehrke

See [LICENSE](LICENSE) file for full license text.

This game is built with [Godot Engine](https://godotengine.org) (MIT License).
For third-party licenses and attributions, see [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md).

## Assets

All game assets under `assets/` were created in-house using an AI tool and are released under
CC0 1.0 (Public Domain). See [ASSETS_LICENSE.md](ASSETS_LICENSE.md) for details.
