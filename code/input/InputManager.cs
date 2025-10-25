// SPDX-License-Identifier: MIT
using System;
using Godot;

public partial class InputManager : Node, ILifecycleScope
{
    /// <inheritdoc/>
    public ServiceLifecycle Lifecycle => ServiceLifecycle.Session;

    public enum InputMode
    {
        None,
        Build,
        BuyLand,
        SellLand,
        Transport,
        Demolish,
    }

    // Keine NodePath-DI mehr: Alle Abhängigkeiten werden über den ServiceContainer aufgelöst
    private bool signaleAktiv = true;

    [Export]
    public bool SignaleAktiv
    {
        get => this.signaleAktiv;
        set
        {
            this.signaleAktiv = value;
            this.AktualisiereSignalWeitergabe();
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

    public InputMode CurrentMode => this.toolManager?.CurrentMode ?? InputMode.None;

    public string CurrentBuildType => this.toolManager?.CurrentBuildType ?? string.Empty;

    private bool initialized = false;

    /// <inheritdoc/>
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
        this.toolManager?.SetMode(mode, buildType);
    }

    public void SetBuildMode(string buildId)
    {
        this.SetMode(InputMode.Build, buildId);
    }

    public void HandleClick(Vector2I zelle)
    {
        this.inputEventRouter?.HandleClick(zelle);
    }

    public IInputTool? GetCurrentTool()
    {
        return this.toolManager?.HoleAktuellesWerkzeug();
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
        if (this.initialized)
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
        this.CallDeferred(nameof(this.InitializeDeferred));
    }

    private void InitializeDeferred()
    {
        if (this.initialized)
        {
            return;
        }

        try
        {
            // Kinder-Nodes holen
            this.FindeKinder();

            // Komponenten verdrahten
            this.VerbindeKomponenten();
            this.AktualisiereSignalWeitergabe();

            this.initialized = true;
            DebugLogger.LogInput("InputManager: Initialisierung abgeschlossen (DI, deferred)");
        }
        catch (System.Exception ex)
        {
            DebugLogger.Error("debug_input", "InitializeDeferredFailed", ex.Message);
        }
    }

    private void FindeKinder()
    {
        this.inputHandler = this.GetNode<InputHandler>("InputHandler");
        this.toolManager = this.GetNode<ToolManager>("InputHandler/ToolManager");
        this.inputEventRouter = this.GetNode<InputEventRouter>("InputHandler/InputEventRouter");
    }

    private void VerbindeKomponenten()
    {
        this.toolManager.InjiziereDependencies(this.landManager, this.buildingManager, this.economyManager, this.transportManager, this.roadManager, this.map, this.eventHub);
        this.inputEventRouter.InjiziereDependencies(this.map, this.gameManager, this.toolManager, this.buildingManager, this.kameraController, this.eventHub);
        this.inputHandler.InjiziereDependencies(this.map, this.inputEventRouter, this.toolManager);
    }

    private void AktualisiereSignalWeitergabe()
    {
        this.toolManager?.SetzeSignaleAktiv(this.signaleAktiv);
        this.inputEventRouter?.SetzeSignaleAktiv(this.signaleAktiv);
    }
}

