// SPDX-License-Identifier: MIT
using System;
using Godot;

/// <summary>
/// BootSelfTest: Fruehe Start-Pruefungen fuer Autoload-Reihenfolge, DI-Verfuegbarkeit und Godot/.NET-Versionen.
/// Wird als Autoload nach UIService registriert und prueft Services fail-fast in Dev-Builds.
/// </summary>
public partial class BootSelfTest : Node
{
    [Export]
    public bool StopOnErrorInDev { get; set; } = true; // In Debug-Builds bei fatalen Fehlern stoppen

    [Export]
    public bool LogDetails { get; set; } = true;

    // In Release-Builds standardmäßig deaktiviert, damit der Self-Test dort harmlos ist.
    [Export]
    public bool RunInRelease { get; set; } = false;

    public override void _Ready()
    {
        // In Release-Builds ggf. keinen Check ausführen
        if (OS.HasFeature("release") && !this.RunInRelease)
        {
            if (this.LogDetails)
            {
                DebugLogger.Info("debug_services", "BootSelfTestDisabled", "BootSelfTest disabled in release build");
            }

            return;
        }
        // Einen Frame warten, damit alle Autoloads _Ready() ausgeführt haben
        this.CallDeferred(nameof(this.RunChecks));
    }

    private void RunChecks()
    {
        bool ok = true;
        ok &= this.CheckGodotVersion();
        ok &= this.CheckServices();
        ok &= this.ValidateNoDIViolations(); // Phase 7: DI pattern validation
        this.CallDeferred(nameof(this.RunLateChecks));

        if (!ok && this.StopOnErrorInDev && OS.IsDebugBuild())
        {
            DebugLogger.Error("debug_services", "BootSelfTestFatal", "Fatal startup checks failed. Quitting (Dev mode)");
            this.GetTree().Quit(120); // nicht-Null Exit-Code für CI/Logs
        }
        else if (ok && this.LogDetails)
        {
            DebugLogger.Info("debug_services", "BootSelfTestPassed", "Startup checks passed");
        }
    }

    private bool CheckGodotVersion()
    {
        try
        {
            var v = Engine.GetVersionInfo();
            int major = (int)v["major"];
            int minor = (int)v["minor"];
            if (major != 4 || minor != 4)
            {
                DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => $"[BT010] Godot-Version erwartet 4.4.x, gefunden {major}.{minor}");
                return false;
            }
            if (this.LogDetails)
            {
                DebugLogger.LogServices(() => $"BootSelfTest: Godot {major}.{minor} erkannt.");
            }

            return true;
        }
        catch (Exception ex)
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "[BT011] Godot-Version konnte nicht ermittelt werden: " + ex.Message);
            return false;
        }
    }

    private bool CheckServices()
    {
        bool ok = true;
        var root = this.GetTree().Root;

        // Autoload-Anwesenheit prüfen
        Node? scNode = root.GetNodeOrNull<Node>("/root/ServiceContainer");
        if (scNode == null)
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "[BT001] ServiceContainer Autoload fehlt");
            ok = false;
        }
        if (root.GetNodeOrNull<Node>("/root/DevFlags") == null)
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "[BT002] DevFlags Autoload fehlt");
            ok = false;
        }
        if (root.GetNodeOrNull<Node>("/root/EventHub") == null)
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "[BT003] EventHub Autoload fehlt");
            ok = false;
        }
        if (root.GetNodeOrNull<Node>("/root/Database") == null)
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "[BT004] Database Autoload fehlt");
            ok = false;
        }
        if (root.GetNodeOrNull<Node>("/root/UIService") == null)
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "[BT005] UIService Autoload fehlt");
            ok = false;
        }
        if (root.GetNodeOrNull<Node>("/root/DataIndex") == null)
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "[BT016] DataIndex Autoload fehlt");
            ok = false;
        }

        // DI-Verfügbarkeit über ServiceContainer prüfen
        var sc = ServiceContainer.Instance;
        if (sc == null)
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "[BT006] ServiceContainer.Instance ist null");
            ok = false;
        }
        else
        {
            // Named Services
            if (sc.GetNamedService<Node>("EventHub") == null)
            {
                DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "[BT007] EventHub nicht im ServiceContainer registriert");
                ok = false;
            }
            if (sc.GetNamedService<Database>("Database") == null)
            {
                DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "[BT008] Database nicht im ServiceContainer registriert");
                ok = false;
            }
            if (sc.GetNamedService<UIService>("UIService") == null)
            {
                DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "[BT009] UIService nicht im ServiceContainer registriert");
                ok = false;
            }
            // Release-Schutz: Legacy-Fallbacks duerfen in Release nicht aktiv sein
            var db = sc.GetNamedService<Database>("Database");
            if (db != null && OS.HasFeature("release") && db.AllowLegacyFallbackInRelease)
            {
                DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "[BT018] Legacy-Fallbacks in Release aktiviert - bitte in Database ausschalten");
                ok = false;
            }
            // Zusatzchecks (Phase 4)
            // GameManager ist kein Autoload: nur pruefen, wenn ein GameManager-Node im Baum vorhanden ist
            var gmNode = root.FindChild("GameManager", true, false);
            if (gmNode != null && sc.GetNamedService<GameManager>("GameManager") == null)
            {
                DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "[BT012] GameManager nicht im ServiceContainer registriert");
                ok = false;
            }
            if (sc.GetNamedService<UIService>(ServiceNames.UIService) == null)
            {
                DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "[BT013] UIService nicht als typisierter Service im ServiceContainer enthalten");
                ok = false;
            }
        }

        // Instanzkonsistenz: ServiceContainer.Instance zeigt auf Autoload-Node?
        if (ServiceContainer.Instance == null || scNode == null || !ReferenceEquals(ServiceContainer.Instance, scNode))
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "[BT014] ServiceContainer.Instance stimmt nicht mit Autoload-Node überein");
            ok = false;
        }

        if (ok && this.LogDetails)
        {
            DebugLogger.LogServices("BootSelfTest: Autoload/DI-Checks OK.");
        }

        return ok;
    }

    private async void RunLateChecks()
    {
        // Ein paar Frames warten, damit Szene und GameManager geladen werden können
        await this.ToSignal(this.GetTree(), SceneTree.SignalName.ProcessFrame);
        await this.ToSignal(this.GetTree(), SceneTree.SignalName.ProcessFrame);

        try
        {
            var sc = ServiceContainer.Instance;
            if (sc == null)
            {
                return;
            }

            // GameManager finden; wenn keiner existiert (z. B. Hauptmenü), keine Session-Checks erzwingen
            var gm = sc.GetNamedService<GameManager>("GameManager") ?? this.GetTree().Root.FindChild("GameManager", true, false) as GameManager;
            if (gm == null)
            {
                DebugLogger.LogServices("[BootSelfTest] Kein GameManager im Baum – Session-Checks werden uebersprungen.");
                return;
            }

            // Noch ein paar Frames Zeit geben, damit DIContainer registrieren kann
            for (int i = 0; i < 3 && !gm.IsCompositionComplete; i++)
            {
                await this.ToSignal(this.GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            // Falls die Komposition noch nicht abgeschlossen ist, Checks überspringen
            if (!gm.IsCompositionComplete)
            {
                var simEarly = sc.GetNamedService<Simulation>(ServiceNames.Simulation);
                if (simEarly != null && simEarly.IstAktiv)
                {
                    DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "[BT020] Simulation vor Abschluss der Komposition gestartet");
                }
                DebugLogger.LogServices("[BootSelfTest] Komposition noch nicht abgeschlossen - spaete DI-Checks uebersprungen.");
                return;
            }

            // Erwartete Session-Services (typisiert) pruefen
            bool allOk = true;
            allOk &= this.CheckTypedService<LandManager>("LandManager");
            allOk &= this.CheckTypedService<BuildingManager>("BuildingManager");
            allOk &= this.CheckTypedService<TransportManager>("TransportManager");
            allOk &= this.CheckTypedService<RoadManager>("RoadManager");
            allOk &= this.CheckTypedService<EconomyManager>("EconomyManager");
            allOk &= this.CheckTypedService<InputManager>("InputManager");
            allOk &= this.CheckTypedService<ResourceManager>("ResourceManager");
            allOk &= this.CheckTypedService<ProductionManager>("ProductionManager");
            allOk &= this.CheckTypedService<GameClockManager>("GameClockManager");
            allOk &= this.CheckTypedService<Simulation>("Simulation");

            if (!allOk && this.StopOnErrorInDev && OS.IsDebugBuild())
            {
                DebugLogger.Error("debug_services", "BootSelfTestTypedServiceMissing", "Typed service registration incomplete. Quitting (Dev mode)");
                this.GetTree().Quit(302);
                return;
            }

            // Komposition vs. Simulation Start pruefen
            var sim = sc.GetNamedService<Simulation>(ServiceNames.Simulation);
            if (sim != null && !sim.IstAktiv)
            {
                DebugLogger.LogServices("[BootSelfTest] Hinweis: Simulation ist nach Komposition noch nicht aktiv.");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Warn, () => $"[BT021] Late checks failed: {ex.Message}");
        }
    }

    private bool CheckTypedService<T>(string name)
        where T : Node
    {
        var svc = ServiceContainer.Instance?.GetNamedService<T>(typeof(T).Name);
        if (svc == null)
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => $"[BT019] {name} nicht als typisierter Service registriert");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Phase 7: Validates that all managers follow the explicit DI pattern.
    /// Checks:
    /// - All managers have Initialize() method
    /// - All session-scoped managers implement ILifecycleScope
    /// - DIContainer exists and is properly configured.
    /// </summary>
    private bool ValidateNoDIViolations()
    {
        bool ok = true;

        try
        {
            // Find GameManager (if not in scene yet, skip this check)
            var root = this.GetTree().Root;
            var gmNode = root.FindChild("GameManager", true, false) as GameManager;

            if (gmNode == null)
            {
                // No GameManager in scene yet - skip manager validation
                if (this.LogDetails)
                {
                    DebugLogger.LogServices("[BootSelfTest] DI validation skipped - GameManager not in scene yet");
                }

                return true;
            }

            // Check if DIContainer exists as child of GameManager
            var diContainer = gmNode.GetNodeOrNull<Node>("DIContainer");
            if (diContainer == null)
            {
                DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => "[BT022] DIContainer nicht als Kind von GameManager gefunden");
                ok = false;
            }

            // List of managers that should have Initialize() and ILifecycleScope
            var managerTypes = new[]
            {
                typeof(EconomyManager),
                typeof(LandManager),
                typeof(BuildingManager),
                typeof(TransportManager),
                typeof(RoadManager),
                typeof(InputManager),
                typeof(ResourceManager),
                typeof(ProductionManager),
                typeof(GameClockManager),
                typeof(CityGrowthManager),
                typeof(GameTimeManager),
            };

            // Helper service types
            var helperServiceTypes = new[]
            {
                typeof(LogisticsService),
                typeof(MarketService),
                typeof(SupplierService),
                typeof(ProductionCalculationService),
            };

            // Check each manager type
            foreach (var managerType in managerTypes)
            {
                ok &= this.ValidateManagerHasInitialize(managerType);
                ok &= this.ValidateManagerHasLifecycleScope(managerType, ServiceLifecycle.Session);
            }

            // Check each helper service type
            foreach (var serviceType in helperServiceTypes)
            {
                ok &= this.ValidateManagerHasInitialize(serviceType);
                ok &= this.ValidateManagerHasLifecycleScope(serviceType, ServiceLifecycle.Session);
            }

            // Check autoload services implement ILifecycleScope with Singleton
            var singletonTypes = new[]
            {
                typeof(EventHub),
                typeof(Database),
                typeof(UIService),
            };

            foreach (var singletonType in singletonTypes)
            {
                ok &= this.ValidateManagerHasLifecycleScope(singletonType, ServiceLifecycle.Singleton);
            }

            if (ok && this.LogDetails)
            {
                DebugLogger.LogServices("[BootSelfTest] DI pattern validation passed - all managers follow explicit DI");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error, () => $"[BT023] DI validation failed: {ex.Message}");
            ok = false;
        }

        return ok;
    }

    /// <summary>
    /// Validates that a manager type has an Initialize() method.
    /// </summary>
    private bool ValidateManagerHasInitialize(Type managerType)
    {
        try
        {
            var initMethod = managerType.GetMethod(
                "Initialize",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (initMethod == null)
            {
                DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error,
                    () => $"[BT024] Manager {managerType.Name} fehlt Initialize() Methode (Explicit DI required)");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Warn,
                () => $"[BT025] Fehler beim Prüfen von Initialize() für {managerType.Name}: {ex.Message}");
            return true; // Don't fail hard on reflection errors
        }
    }

    /// <summary>
    /// Validates that a manager type implements ILifecycleScope with the correct lifecycle.
    /// </summary>
    private bool ValidateManagerHasLifecycleScope(Type managerType, ServiceLifecycle expectedLifecycle)
    {
        try
        {
            // Check if type implements ILifecycleScope
            if (!typeof(ILifecycleScope).IsAssignableFrom(managerType))
            {
                DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error,
                    () => $"[BT026] Manager {managerType.Name} implementiert nicht ILifecycleScope");
                return false;
            }

            // For validation, we need an instance - try to find it in the tree
            var root = this.GetTree().Root;
            var instance = root.FindChild(managerType.Name, true, false);

            if (instance == null)
            {
                // Instance not in tree yet - skip lifecycle value check
                if (this.LogDetails)
                {
                    DebugLogger.LogServices($"[BootSelfTest] {managerType.Name} nicht im Baum - Lifecycle-Wert-Check übersprungen");
                }

                return true;
            }

            if (instance is ILifecycleScope scoped)
            {
                if (scoped.Lifecycle != expectedLifecycle)
                {
                    DebugLogger.Log("debug_services", DebugLogger.LogLevel.Error,
                        () => $"[BT027] Manager {managerType.Name} hat falschen Lifecycle: {scoped.Lifecycle}, erwartet: {expectedLifecycle}");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            DebugLogger.Log("debug_services", DebugLogger.LogLevel.Warn,
                () => $"[BT028] Fehler beim Prüfen von ILifecycleScope für {managerType.Name}: {ex.Message}");
            return true; // Don't fail hard on reflection errors
        }
    }
}
