# DI Policy (C# core vs. UI)

Ziel: Echte Dependency Injection im C#-Kern, weg vom Service-Locator.

- Composition Root: `DIContainer` (unter `GameManager`) konstruiert/verdrahtet Manager und ruft deren `Initialize(...)` auf.
- C#-Kern (Manager, Systeme):
  - Erhalten Abhängigkeiten explizit (Konstruktor oder `Initialize(...)`).
  - Keine `ServiceContainer.Instance.*Get*`-Zugriffe im Produktivpfad.
  - Fallbacks über `ServiceContainer` sind nur vorübergehend als Legacy-Notnagel erlaubt.
- UI/GDScript-Bridging:
  - `ServiceContainer` bleibt als Named-Registry für UI/Autoloads (z. B. `EventHub`, `Database`, `UIService`, `GameManager`).
  - Keine typisierten Zugriffe aus UI-Skripten; Named-Services genügen.
- Async/Ordering:
  - Async-Waits (`WaitForNamedService`) nur an UI/Autoload-Kanten. Kern bekommt seine Dependencies synchron beim Aufbau.

Migrationsregeln:
- Schrittweise pro Manager `Initialize(...)` einführen und im `DIContainer` aufrufen.
- Bestehende Service-Locator-Zugriffe entfernen oder hinter Fallback-Codepfad schieben.
- Keine neuen typisierten `RegisterService/GetService`-Nutzungen hinzufügen.

CI-Unterstützung:
- Script `tools/ci/CheckNoServiceLocator.ps1` meldet Verstöße in `code/managers/**`.
- Optionaler Parameter `-FailOnViolation` kann Builds brechen, sobald die Migration abgeschlossen ist.

Status (100% umgesetzt - Stand 2025-02-01):
- ALLE Manager haben Initialize(...) Methoden
- DIContainer ist einzige Composition Root
- ServiceContainer nur noch Named-Registry (keine Typed-APIs mehr)
- Keine Service Locator Pattern mehr im Code
- Helper Services werden über DIContainer verwaltet

---

## Lifecycle & Cleanup Richtlinien

**C#-Events über AboVerwalter kapseln** (`code/runtime/util/AboVerwalter.cs`):
- C#-Events via `Abonniere(() => ev += Handler, () => ev -= Handler)` tracken
- In jedem `Node` in `_ExitTree()` immer `_abos.DisposeAll()` aufrufen

**Services ohne Node-Basis mit Events:**
- Implementieren `IDisposable` und trennen Abos in `Dispose()`
- Beispiel: `TransportEventService` mit `Connect*`/`Disconnect*` und `Dispose()` für sauberes Cleanup

**Vor Scene-/Game-Resets:**
- Immer `TransportManager.ClearAllData()` ausführen:
  - Stoppt laufende Transporte und leert Routen/Warteschlangen
  - Verhindert Ticks/Access auf bereits entsorgte Gebäude
  - Ist in `GameStateOperations.ClearGameState(...)` und `GameManager.StartNewGameDirect(...)` integriert
