# DI & Async Guide (C# / Godot 4)

Kurzfassung der Richtlinien für Dependency Injection, Thread‑Safety und Async‑Nutzung in IndustrieLite.

- Einheitlicher Zugriff über `ServiceContainer` (typisiert/named). Keine `/root`‑Lookups in C#‑Logik.
- ServiceContainer ist thread‑safe (zentrales Lock). Enumerationen erfolgen über Snapshots.
- `WaitForService(..)`/`WaitForNamedService(..)` sind non‑blocking; Overloads mit `CancellationToken`/Timeout verfügbar.
- Bei `ClearAllServices()`/`ClearGameSessionServices()` werden alle ausstehenden `WaitFor*`‑Aufrufe mit `OperationCanceledException` abgebrochen.

Async‑Kontext:
- Node/Manager/Godot‑abhängiger Code: ohne `ConfigureAwait(false)` (Main‑Thread benötigt).
- Bibliotheks-/Hintergrundcode: `ConfigureAwait(false)` verwenden (z. B. `GameServiceLocator`).
- Niemals `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` verwenden.

Warum Godot‑safe Async?
- Godot ist nicht thread‑affin tolerant wie reine .NET‑Apps: Alle Aufrufe an Godot APIs (`Node`, `EmitSignal`, `GetNode`, `QueueRedraw`, etc.) müssen auf dem Hauptthread erfolgen.
- `ConfigureAwait(false)` verschiebt Fortsetzungen potentiell auf beliebige Threadpool‑Threads. In Node/Manager‑Code würde das zu sporadischen Crashes/Undefined Behavior führen.
- Deshalb gilt: In Code, der im Anschluss Godot‑APIs aufruft, kein `ConfigureAwait(false)` verwenden. In Library/Service‑Code (keine Godot‑APIs) ist `ConfigureAwait(false)` erwünscht (Deadlock‑Vermeidung, bessere Skalierung).

Zurück auf den Main‑Thread wechseln
```
// Nach Hintergrundarbeit zurück auf den Godot‑Main‑Thread springen
async Task DoWorkAndReturnToMainAsync(SceneTree tree)
{
    // Hintergrundarbeit
    await SomeIoBoundWorkAsync().ConfigureAwait(false);

    // Eine Frame‑Fortsetzung abwarten → wir sind wieder auf dem Main‑Thread
    await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

    // Ab hier sind Godot‑APIs sicher aufrufbar
    GetTree().Root.PropagateNotification((int)Node.NotificationReady);
}
```

Hilfs‑APIs im ServiceContainer:
- `TryGetService<T>(out T? service)` / `TryGetNamedService<T>(string name, out T? service)`
  - liefert `false` ohne Logging, wenn nicht vorhanden (kein Warnzähler, weniger Log‑Noise).
- `RequireService<T>()` / `RequireNamedService<T>(string name)`
  - wirft `InvalidOperationException` bei fehlender harten Abhängigkeit (Fail‑fast).

Beispiele:
```
// Node/Manager (Main‑Thread)
var sc = ServiceContainer.Instance!;
if (!sc.TryGetNamedService<BuildingManager>(nameof(BuildingManager), out var bm))
    bm = await sc.WaitForNamedService<BuildingManager>(nameof(BuildingManager));

// Hintergrundcode (kein Godot‑API)
var ctx = await locator.GetContextAsync().ConfigureAwait(false);

// Harte Abhängigkeit
var db = sc.RequireNamedService<Database>(ServiceNames.Database);
```
