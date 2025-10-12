// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// BuildingFactory erzeugt Gebaeude-Instanzen datengetrieben aus der Database.
/// Bevorzugt Prefabs, faellt sonst auf bekannte C#-Klassen per ID-Mapping zurueck.
/// Setzt Standard-Properties (GridPos, Size, TileSize, Position) und DefinitionId.
/// </summary>
public class BuildingFactory
{
    private readonly Database? database; // optional
    private readonly ProductionManager? productionManager;
    private readonly EconomyManager? economyManager;
    private readonly EventHub? eventHub;
    private readonly Simulation? simulation;
    private readonly GameTimeManager? gameTimeManager;

    public BuildingFactory(Database? database = null, ProductionManager? productionManager = null, EconomyManager? economyManager = null, EventHub? eventHub = null, Simulation? simulation = null, GameTimeManager? gameTimeManager = null)
    {
        this.database = database;
        this.productionManager = productionManager;
        this.economyManager = economyManager;
        this.eventHub = eventHub;
        this.simulation = simulation;
        this.gameTimeManager = gameTimeManager;
    }

    public Building? Create(string type, Vector2I cell, int tileSize)
    {
        Building? b = null;
        BuildingDef? def = null;
        // 1) Lookup in Database (akzeptiert Legacy-IDs ueber LegacyIds)
        if (database != null)
        {
            def = database.GetBuilding(type);
            DebugLogger.LogServices($"BuildingFactory: Lookup '{type}' => {(def != null ? def.Id : "not found")}");
            if (def == null)
            {
                // Versuche kanonische ID ueber Migration
                var canon = IdMigration.ToCanonical(type);
                if (!string.Equals(canon, type, System.StringComparison.Ordinal))
                {
                    def = database.GetBuilding(canon);
                    DebugLogger.LogServices($"BuildingFactory: Fallback lookup via canonical '{canon}' => {(def != null ? def.Id : "not found")}");
                }
            }
        }

        // 2) Prefab bevorzugen
        if (def != null && def.Prefab != null)
        {
            var node = def.Prefab.Instantiate();
            b = node as Building;
            DebugLogger.LogServices($"BuildingFactory: Created from Prefab: {b?.GetType().Name ?? "null"}");
        }

        // 3) Kein Prefab: typisierte Klasse per ID-Mapping
        if (b == null)
        {
            var id = def?.Id ?? IdMigration.ToCanonical(type);
            switch (id)
            {
                case BuildingIds.House: b = new House(); break;
                case BuildingIds.SolarPlant: b = new SolarPlant(); break;
                case BuildingIds.WaterPump: b = new WaterPump(); break;
                case BuildingIds.ChickenFarm: b = new ChickenFarm(); break;
                case "grain_farm": b = new GrainFarm(); break;
                case "pig_farm": b = new PigFarm(); break;
                case BuildingIds.City: b = new City(); break;
            }
            if (b != null)
                DebugLogger.LogServices($"BuildingFactory: Created from ID mapping: {b.GetType().Name} for '{id}'");
        }

        // 4) Generischer Fallback: unbekannte IDs als Basis-Gebaeude behandeln
        if (b == null)
        {
            b = new Building();
            DebugLogger.LogServices($"BuildingFactory: Fallback to generic Building for '{type}'");
        }

        // Standard-Properties
        b.GridPos = cell;
        if (def != null) b.Size = new Vector2I(def.Width, def.Height);
        else b.Size = b.DefaultSize;
        b.TileSize = tileSize;
        b.Position = new Vector2(cell.X * tileSize, cell.Y * tileSize);

        // DefinitionId setzen (kanonische ID falls verfuegbar, sonst Migration der Eingabe)
        if (def != null) b.DefinitionId = def.Id; else b.DefinitionId = IdMigration.ToCanonical(type);

        // Rezept-Verknüpfung: Standardrezept aus BuildingDef in Gebäude übernehmen (falls unterstützt)
        if (def != null && !string.IsNullOrEmpty(def.DefaultRecipeId))
        {
            switch (b)
            {
                case ChickenFarm cf:
                    if (string.IsNullOrEmpty(cf.RezeptIdOverride)) cf.RezeptIdOverride = def.DefaultRecipeId;
                    break;
                case SolarPlant sp:
                    if (string.IsNullOrEmpty(sp.RezeptIdOverride)) sp.RezeptIdOverride = def.DefaultRecipeId;
                    break;
                case WaterPump wp:
                    if (string.IsNullOrEmpty(wp.RezeptIdOverride)) wp.RezeptIdOverride = def.DefaultRecipeId;
                    break;
            }
        }

        // Database + weitere Dependencies injizieren
        try { b.Initialize(database); } catch { }
        try { b.InitializeDependencies(productionManager, economyManager, eventHub); } catch { }
        if (b is City city)
        {
            try { city.Initialize(eventHub, gameTimeManager, simulation); } catch { }
        }

        return b;
    }
}



