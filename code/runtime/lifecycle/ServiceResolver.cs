// SPDX-License-Identifier: MIT
using System;
using Godot;

/// <summary>
/// Helper-Klasse für Service-Auflösung im GameLifecycleManager.
/// Kapselt die komplexe Service-Initialisierungs-Logik ohne Node-Dependencies.
/// </summary>
internal class ServiceResolver
{
    public class ServiceReferences
    {
        public GameManager? GameManager { get; set; }

        public EconomyManager? EconomyManager { get; set; }

        public LandManager? LandManager { get; set; }

        public BuildingManager? BuildingManager { get; set; }

        public TransportManager? TransportManager { get; set; }

        public RoadManager? RoadManager { get; set; }

        public ResourceManager? ResourceManager { get; set; }

        public ProductionManager? ProductionManager { get; set; }

        public SaveLoadService? SaveLoadService { get; set; }

        public Map? Map { get; set; }

        public EventHub? EventHub { get; set; }

        public GameTimeManager? GameTimeManager { get; set; }

        public bool AreBasicServicesReady()
        {
            return this.EconomyManager != null &&
                   this.LandManager != null &&
                   this.BuildingManager != null &&
                   this.ProductionManager != null;
        }

        public bool AreAllServicesReady()
        {
            return this.AreBasicServicesReady() &&
                   this.SaveLoadService != null &&
                   this.GameManager != null;
        }
    }

    private readonly Node ownerNode;

    public ServiceResolver(Node ownerNode)
    {
        this.ownerNode = ownerNode ?? throw new ArgumentNullException(nameof(ownerNode));
    }

    /// <summary>
    /// Versucht alle Services aufzulösen. Gibt null zurück wenn nicht alle verfügbar sind.
    /// </summary>
    /// <returns></returns>
    public ServiceReferences? TryResolveServices()
    {
        var refs = new ServiceReferences();

        // GameManager direkt vom Parent holen (zuverlässiger)
        refs.GameManager = this.ownerNode.GetNodeOrNull<GameManager>("../");
        if (refs.GameManager == null)
        {
            DebugLogger.LogLifecycle("ServiceResolver: GameManager not found");
            return null;
        }

        // Manager direkt beim GameManager holen
        refs.EconomyManager = refs.GameManager.EconomyManager;
        refs.LandManager = refs.GameManager.LandManager;
        refs.BuildingManager = refs.GameManager.BuildingManager;
        refs.TransportManager = refs.GameManager.TransportManager;
        refs.RoadManager = refs.GameManager.RoadManager;
        refs.ResourceManager = refs.GameManager.ResourceManager;
        refs.ProductionManager = refs.GameManager.ProductionManager;

        // Basis-Services prüfen
        if (!refs.AreBasicServicesReady())
        {
            DebugLogger.LogLifecycle("ServiceResolver: Manager-Referenzen noch nicht verfügbar");
            return null;
        }

        // SaveLoadService auflösen
        refs.SaveLoadService = refs.GameManager?.SaveLoadService ??
                              this.ownerNode.GetNodeOrNull<SaveLoadService>("../SaveLoadService");
        if (refs.SaveLoadService == null)
        {
            var sc = ServiceContainer.Instance;
            if (sc != null)
            {
                SaveLoadService? sl;
                if (sc.TryGetNamedService<SaveLoadService>(nameof(SaveLoadService), out sl))
                {
                    refs.SaveLoadService = sl;
                }
            }
        }
        if (refs.SaveLoadService == null)
        {
            DebugLogger.LogLifecycle("ServiceResolver: SaveLoadService nicht verfügbar");
            return null;
        }

        // Map auflösen
        refs.Map = refs.GameManager?.GetNodeOrNull<Map>("../Map");
        if (refs.Map == null)
        {
            DebugLogger.LogLifecycle("ServiceResolver: Map nicht gefunden");
            return null;
        }

        // EventHub auflösen
        var scEh = ServiceContainer.Instance;
        if (scEh != null)
        {
            EventHub? eh;
            if (scEh.TryGetNamedService<EventHub>("EventHub", out eh))
            {
                refs.EventHub = eh;
            }
        }

        // GameTimeManager auflösen
        if (scEh != null)
        {
            GameTimeManager? gtm;
            if (scEh.TryGetNamedService<GameTimeManager>("GameTimeManager", out gtm))
            {
                refs.GameTimeManager = gtm;
            }
        }

        DebugLogger.LogLifecycle("ServiceResolver: Alle Services erfolgreich aufgelöst");
        return refs;
    }

    /// <summary>
    /// Emit Money-Changed Event über EventHub.
    /// </summary>
    public void EmitMoneyChanged(ServiceReferences services)
    {
        var sc2 = ServiceContainer.Instance;
        EventHub? eventHub = null;
        if (sc2 != null)
        {
            sc2.TryGetNamedService<EventHub>("EventHub", out eventHub);
        }

        if (eventHub != null && services.EconomyManager != null)
        {
            eventHub.EmitSignal(EventHub.SignalName.MoneyChanged, services.EconomyManager.GetMoney());
        }
    }

    /// <summary>
    /// GameTime zurücksetzen.
    /// </summary>
    public void ResetGameTime()
    {
        var sc3 = ServiceContainer.Instance;
        GameTimeManager? gtm = null;
        if (sc3 != null)
        {
            sc3.TryGetNamedService<GameTimeManager>("GameTimeManager", out gtm);
        }

        gtm?.ResetToStart();
    }
}
