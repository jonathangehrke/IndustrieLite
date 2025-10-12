# CI Tools - Phase 7: DI Pattern Enforcement

This directory contains CI scripts to enforce architectural patterns and prevent regressions.

## CheckNoRuntimeServiceLocator.ps1

**Purpose**: Ensures all managers follow explicit Dependency Injection pattern by detecting runtime ServiceContainer lookups.

**Usage**:
```powershell
# Basic check (from project root)
powershell.exe -ExecutionPolicy Bypass -File tools\ci\CheckNoRuntimeServiceLocator.ps1

# With verbose output
powershell.exe -ExecutionPolicy Bypass -File tools\ci\CheckNoRuntimeServiceLocator.ps1 -Verbose

# Custom root path
powershell.exe -ExecutionPolicy Bypass -File tools\ci\CheckNoRuntimeServiceLocator.ps1 -RootPath "C:\path\to\project"
```

**Exit Codes**:
- `0`: Success - No violations found
- `1`: Failure - Runtime ServiceContainer lookups detected

**What it checks**:
- Scans `code/managers/*.cs` and `code/runtime/*.cs` for runtime ServiceContainer lookups
- Violations: `ServiceContainer.Instance?.GetNamedService()`, `GetService()`, `WaitForService()`
- Excludes: `Initialize.cs` files, `DIContainer.cs`, `ServiceContainer.cs`, self-registration patterns

**Integration with CI**:

### GitHub Actions
```yaml
name: DI Pattern Check
on: [push, pull_request]
jobs:
  check-di-pattern:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - name: Check DI Pattern
        run: |
          powershell.exe -ExecutionPolicy Bypass -File tools\ci\CheckNoRuntimeServiceLocator.ps1
```

### Pre-commit Hook
```bash
#!/bin/bash
# .git/hooks/pre-commit
echo "Running DI pattern check..."
powershell.exe -ExecutionPolicy Bypass -File tools/ci/CheckNoRuntimeServiceLocator.ps1
if [ $? -ne 0 ]; then
    echo "DI pattern violations found. Commit rejected."
    exit 1
fi
```

## BootSelfTest Runtime Validation

The `BootSelfTest` autoload performs runtime DI validation during game startup:

**What it validates** (Phase 7):
- ✅ All managers have `Initialize()` methods (explicit DI)
- ✅ All session-scoped managers implement `ILifecycleScope` with `Session` lifecycle
- ✅ All singleton services implement `ILifecycleScope` with `Singleton` lifecycle
- ✅ DIContainer exists as child of GameManager

**Managers checked**:
- Core: EconomyManager, LandManager, BuildingManager, TransportManager, RoadManager
- Production: ResourceManager, ProductionManager, GameTimeManager
- System: InputManager, GameClockManager, CityGrowthManager

**Helper Services checked**:
- LogisticsService, MarketService, SupplierService, ProductionCalculationService

**Error codes** (BT022-BT028):
- `[BT022]`: DIContainer missing from GameManager
- `[BT023]`: General DI validation failure
- `[BT024]`: Manager missing Initialize() method
- `[BT025]`: Reflection error checking Initialize()
- `[BT026]`: Manager doesn't implement ILifecycleScope
- `[BT027]`: Manager has wrong lifecycle scope
- `[BT028]`: Reflection error checking ILifecycleScope

**Configuration**:
```gdscript
# project.godot autoload
BootSelfTest="*res://code/runtime/BootSelfTest.cs"
```

Export variables:
- `StopOnErrorInDev` (bool, default=true): Quit on errors in debug builds
- `LogDetails` (bool, default=true): Print detailed validation logs
- `RunInRelease` (bool, default=false): Enable checks in release builds

## Best Practices

1. **Run CI check before commits**: Use pre-commit hooks to catch violations early
2. **Fix violations immediately**: Don't merge PRs with DI pattern violations
3. **Follow explicit DI**: Always use `Initialize()` parameters, never runtime lookups in manager logic
4. **Self-registration only**: Only use ServiceContainer in `_Ready()` for self-registration
5. **Implement ILifecycleScope**: All services must declare their lifecycle scope

## See Also

- `/docs/DI-POLICY.md` - Dependency Injection guidelines
- `/docs/DI.md` - DI architecture overview
- `/Analyse2.md` - Phase 7 implementation plan (lines 462-502)
