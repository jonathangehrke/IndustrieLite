// SPDX-License-Identifier: MIT
using Godot;
using System;

public partial class InputManager : Node, ILifecycleScope
{
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;
    public enum InputMode { None, Build, BuyLand, SellLand, Transport, Demolish }

    // Keine NodePath-DI mehr: Alle Abhängigkeiten werden über den ServiceContainer aufgelöst

    private bool signaleAktiv = true;

    [Export]
    public bool SignaleAktiv
    {
        get => signaleAktiv;
        set
        {
            signaleAktiv = value;
            AktualisiereSignalWeitergabe();
        }
    }

    private LandManager landManager = default!;
    private BuildingManager buildingManager = default!;
    private TransportManager transportManager = default!;
    private EconomyManager economyManager = default!;
    private RoadManager? roadManager;
    private Map map = default!;
    private GameManager gameManager = default!;
    private EventHub? eventHub;
    private CameraController? kameraController;

    private InputHandler inputHandler = default!;
    private ToolManager toolManager = default!;
    private InputEventRouter inputEventRouter = default!;

    public InputMode CurrentMode => toolManager?.CurrentMode ?? InputMode.None;
    public string CurrentBuildType => toolManager?.CurrentBuildType ?? string.Empty;

    private bool _initialized = false;

    public override void _Ready()
    {
        // Named-Self-Registration für GDScript-Bridge
        var sc = ServiceContainer.Instance;
        if (sc != null)
        {
            try
            {
                sc.RegisterNamedService(nameof(InputManager), this);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error("debug_input", "RegisterWithServiceContainerFailed", ex.Message);
            }
        }
    }

    public void SetMode(InputMode mode, string buildType = "")
    {
        toolManager?.SetMode(mode, buildType);
    }

    public void SetBuildMode(string buildId)
    {
        SetMode(InputMode.Build, buildId);
    }

    public void HandleClick(Vector2I zelle)
    {
        inputEventRouter?.HandleClick(zelle);
    }

    public IInputTool? GetCurrentTool()
    {
        return toolManager?.HoleAktuellesWerkzeug();
    }

    public void Initialize(
        LandManager landManager,
        BuildingManager buildingManager,
        EconomyManager economyManager,
        TransportManager transportManager,
        RoadManager? roadManager,
        Map map,
        GameManager gameManager,
        EventHub? eventHub,
        CameraController? kameraController,
        Simulation? simulation)
    {
        if (_initialized)
        {
            DebugLogger.LogInput("InputManager.Initialize(): Bereits initialisiert, überspringe");
            return;
        }

        this.landManager = landManager;
        this.buildingManager = buildingManager;
        this.economyManager = economyManager;
        this.transportManager = transportManager;
        this.roadManager = roadManager;
        this.map = map;
        this.gameManager = gameManager;
        this.eventHub = eventHub;
        this.kameraController = kameraController;

        // WICHTIG: Warten bis _Ready() komplett ist und Child-Nodes verfügbar sind
        CallDeferred(nameof(InitializeDeferred));
    }

    private void InitializeDeferred()
    {
        if (_initialized) return;

        try
        {
            // Kinder-Nodes holen
            FindeKinder();

            // Komponenten verdrahten
            VerbindeKomponenten();
            AktualisiereSignalWeitergabe();

            _initialized = true;
            DebugLogger.LogInput("InputManager: Initialisierung abgeschlossen (DI, deferred)");
        }
        catch (System.Exception ex)
        {
            DebugLogger.Error("debug_input", "InitializeDeferredFailed", ex.Message);
        }
    }

    private void FindeKinder()
    {
        inputHandler = GetNode<InputHandler>("InputHandler");
        toolManager = GetNode<ToolManager>("InputHandler/ToolManager");
        inputEventRouter = GetNode<InputEventRouter>("InputHandler/InputEventRouter");
    }

    private void VerbindeKomponenten()
    {
        toolManager.InjiziereDependencies(landManager, buildingManager, economyManager, transportManager, roadManager, map, eventHub);
        inputEventRouter.InjiziereDependencies(map, gameManager, toolManager, buildingManager, kameraController, eventHub);
        inputHandler.InjiziereDependencies(map, inputEventRouter, toolManager);
    }

    private void AktualisiereSignalWeitergabe()
    {
        toolManager?.SetzeSignaleAktiv(signaleAktiv);
        inputEventRouter?.SetzeSignaleAktiv(signaleAktiv);
    }
}

