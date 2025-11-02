# Quick Start Guide

## For Players

### Download & Play
1. Visit [IndustrieLite on itch.io](https://jonathangehrke.itch.io/industrielite)
2. Download for your platform (Windows/Linux/Mac)
3. Extract and run `IndustrieLite.exe`
4. Follow the in-game tutorial

### Controls
- **Left Click** - Select/Place buildings
- **Right Click** - Cancel/Deselect
- **WASD / Arrow Keys** - Move camera
- **Mouse Wheel** - Zoom in/out
- **ESC** - Open menu

---

## For Developers

### Prerequisites

**Required:**
- [Godot 4.3+](https://godotengine.org/download) (Mono/.NET build)
- [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
- Git

**Recommended:**
- Visual Studio Code or JetBrains Rider
- C# Dev Kit extension (VS Code)

### Initial Setup (5 minutes)

```bash
# 1. Clone repository
git clone https://github.com/jonathangehrke/IndustrieLite-dev.git
cd IndustrieLite-dev

# 2. Restore .NET dependencies
dotnet restore

# 3. Build C# project
dotnet build

# 4. Open in Godot
# File → Import Project → Select project.godot
# Or: godot --path . --editor

# 5. Run the game
# Press F5 in Godot Editor
```

### Development Workflow

**1. Code → Build → Test**
```bash
# Build C# project
dotnet build

# Run tests
dotnet test

# Format code
dotnet format
```

**2. Running in Editor**
- Open Godot Editor
- Press **F5** to run
- Press **F6** to run current scene
- Console output appears in Godot "Output" panel

**3. Debugging C#**
- Set breakpoints in Rider/VS Code
- Attach debugger to Godot process
- See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for detailed setup

### Project Structure Overview

```
code/
├── buildings/      # Building logic & services (PlacementService, BuildingFactory)
├── managers/       # Core managers (BuildingManager, EconomyManager, TransportManager)
├── runtime/        # Runtime services (DIContainer, UIService, Database, EventHub)
├── sim/            # Simulation systems (ProductionSystem, ITickable)
├── transport/      # Transport subsystem (trucks, orders, routing)
└── common/         # Shared utilities & constants

ui/                 # GDScript UI components
scenes/             # Godot scenes
data/               # Game data (.tres resource files)
docs/               # Documentation
tests/              # Unit & integration tests
```

### First Contribution

**Step 1: Read Documentation**
- [ARCHITECTURE.md](ARCHITECTURE.md) - System overview & architecture decisions
- [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) - Development guidelines & DI patterns
- [CONTRIBUTING.md](CONTRIBUTING.md) - Contribution workflow

**Step 2: Pick a Task**
- Check [GitHub Issues](https://github.com/jonathangehrke/IndustrieLite-dev/issues)
- Look for `good-first-issue` label
- Or explore TODOs in code: `grep -r "TODO:" code/`

**Step 3: Development**
```bash
# Create feature branch
git checkout -b feature/your-feature-name

# Make changes
# ... edit code ...

# Format & test
dotnet format
dotnet test

# Commit with conventional commits
git commit -m "feat: add new building type"
# or
git commit -m "fix: resolve placement validation bug"
```

**Step 4: Submit PR**
- Push to your fork
- Open Pull Request
- CI will run tests & checks
- Address review feedback

### Common Issues & Solutions

#### Build Fails

**Problem:** `error CS0246: The type or namespace 'Godot' could not be found`

**Solution:**
- Ensure you're using Godot 4 **Mono/.NET** build (not standard build)
- Run `dotnet restore` in project root
- Rebuild Godot project (Project → Tools → C# → Build Project)

#### Missing Dependencies

**Problem:** `Could not load file or assembly 'System.Collections.Immutable'`

**Solution:**
```bash
dotnet restore
dotnet clean
dotnet build
```

#### Godot Version Mismatch

**Problem:** `Scene/Node incompatibility errors`

**Solution:**
- Ensure Godot 4.3+ (check Help → About)
- Re-import project (delete `.godot/` folder, restart editor)

#### Tests Fail

**Problem:** `Could not find node '/root/ServiceContainer'`

**Solution:**
- Tests expect Godot runtime environment
- Run via: `dotnet test` (uses Godot headless)
- Or run in Godot: Scene → Run Test Scene

### Debugging Tips

**1. Enable Verbose Logging**
```csharp
// Set in code/runtime/util/DebugLogger.cs
DebugLogger.EnableCategory("Economy");
DebugLogger.EnableCategory("Transport");
DebugLogger.EnableCategory("Production");
```

**2. Inspect State at Runtime**
```csharp
// In any C# file
GD.Print($"Building count: {buildingManager.Buildings.Count}");
DebugLogger.LogInfo("Custom", () => $"Resource: {resourceAmount}");
```

**3. Use Godot Debugger**
- Scene → Remote → Remote Inspector
- Shows live node tree & properties
- Monitors performance metrics

**4. Test Specific Scenarios**
- Create test scenes in `scenes/tests/`
- Write reproduction scripts in GDScript
- See existing examples in `tests/`

### Key Concepts

**Dependency Injection**
- Services are injected via constructor
- `DIContainer` wires all dependencies at startup
- No `new` for managers/services (use DI)

**Event-Driven**
- Use `EventHub` for cross-system communication
- Subscribe: `eventHub.Subscribe(EventNames.BuildingPlaced, OnBuildingPlaced)`
- Publish: `eventHub.Publish(EventNames.BuildingPlaced, building)`

**Result Pattern**
- Methods return `Result<T>` or `Result`
- Check success: `if (result.IsSuccess) { var value = result.Value; }`
- Handle errors: `if (result.IsFailure) { var error = result.ErrorCode; }`

**Deterministic Simulation**
- All game logic runs at fixed 20Hz via `ITickable.Tick(deltaTime)`
- UI runs at display refresh rate (uncapped)
- Ensures consistent behavior for save/load

### Next Steps

1. ✅ Run the game (F5 in Godot)
2. ✅ Explore codebase (start with `code/managers/`)
3. ✅ Read architecture docs ([ARCHITECTURE.md](ARCHITECTURE.md))
4. ✅ Pick a TODO or issue
5. ✅ Submit your first PR!

### Getting Help

- **Documentation:** [docs/](docs/) folder
- **Issues:** [GitHub Issues](https://github.com/jonathangehrke/IndustrieLite-dev/issues)
- **Discussions:** [GitHub Discussions](https://github.com/jonathangehrke/IndustrieLite-dev/discussions)

Welcome to IndustrieLite development! 🚀
