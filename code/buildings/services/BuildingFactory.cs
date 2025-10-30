// SPDX-License-Identifier: MIT
using System;
using Godot;

/// <summary>
/// BuildingFactory erzeugt Gebaeude-Instanzen datengetrieben aus der Database.
/// Bevorzugt Prefabs, faellt sonst auf bekannte C#-Klassen per ID-Mapping zurueck.
/// Setzt Standard-Properties (GridPos, Size, TileSize, Position) und DefinitionId.
/// </summary>
public class BuildingFactory
{
    private readonly Database database; // required
    private readonly ProductionManager? productionManager;
    private readonly EconomyManager? economyManager;
    private readonly EventHub? eventHub;
    private readonly Simulation? simulation;
    private readonly GameTimeManager? gameTimeManager;
    private readonly Node? dataIndex;

    public BuildingFactory(Database database, ProductionManager? productionManager = null, EconomyManager? economyManager = null, EventHub? eventHub = null, Simulation? simulation = null, GameTimeManager? gameTimeManager = null, Node? dataIndex = null)
    {
        this.database = database ?? throw new ArgumentNullException(nameof(database), "BuildingFactory requires Database for BuildingDef lookups");
        this.productionManager = productionManager;
        this.economyManager = economyManager;
        this.eventHub = eventHub;
        this.simulation = simulation;
        this.gameTimeManager = gameTimeManager;
        this.dataIndex = dataIndex;
    }

    public Building? Create(string type, Vector2I cell, int tileSize)
    {
        Building? b = null;
        BuildingDef? def = null;

        // 1) Lookup in Database (akzeptiert Legacy-IDs ueber LegacyIds)
        def = this.database.GetBuilding(type);
        DebugLogger.LogServices($"BuildingFactory: Lookup '{type}' => {(def != null ? def.Id : "not found")}");
        if (def == null)
        {
            // Versuche kanonische ID ueber Migration
            var canon = IdMigration.ToCanonical(type);
            if (!string.Equals(canon, type, System.StringComparison.Ordinal))
            {
                def = this.database.GetBuilding(canon);
                DebugLogger.LogServices($"BuildingFactory: Fallback lookup via canonical '{canon}' => {(def != null ? def.Id : "not found")}");
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

            // Spezielle Gebaeude mit eigener Logik
            switch (id)
            {
                case BuildingIds.House: b = new House(); break;
                case BuildingIds.SolarPlant: b = new SolarPlant(); break;
                case BuildingIds.WaterPump: b = new WaterPump(); break;
                case BuildingIds.City: b = new City(); break;
            }

            // Produktionsgebaeude (Farmen etc.) verwenden GenericProductionBuilding
            if (b == null && def != null && !string.IsNullOrEmpty(def.DefaultRecipeId))
            {
                b = new GenericProductionBuilding();
                DebugLogger.LogServices($"BuildingFactory: Created GenericProductionBuilding for '{id}'");
            }
            else if (b != null)
            {
                DebugLogger.LogServices($"BuildingFactory: Created from ID mapping: {b.GetType().Name} for '{id}'");
            }
        }

        // 4) Generischer Fallback: unbekannte IDs als Basis-Gebaeude behandeln
        if (b == null)
        {
            b = new Building();
            DebugLogger.LogServices($"BuildingFactory: Fallback to generic Building for '{type}'");
        }

        // Standard-Properties
        b.GridPos = cell;
        if (def != null)
        {
            b.Size = new Vector2I(def.Width, def.Height);
        }
        else
        {
            b.Size = b.DefaultSize;
        }

        b.TileSize = tileSize;
        b.Position = new Vector2(cell.X * tileSize, cell.Y * tileSize);

        // DefinitionId setzen (kanonische ID falls verfuegbar, sonst Migration der Eingabe)
        if (def != null)
        {
            b.DefinitionId = def.Id;
        }
        else
        {
            b.DefinitionId = IdMigration.ToCanonical(type);
        }

        // Rezept-Verknüpfung: Standardrezept aus BuildingDef in Gebäude übernehmen (falls unterstützt)
        // Generisch für alle Gebäude mit RezeptIdOverride-Property
        if (def != null && !string.IsNullOrEmpty(def.DefaultRecipeId))
        {
            // Versuche, RezeptIdOverride per Reflection zu setzen (funktioniert für alle Production Buildings)
            var rezeptProp = b.GetType().GetProperty("RezeptIdOverride");
            if (rezeptProp != null && rezeptProp.CanWrite)
            {
                var currentValue = rezeptProp.GetValue(b) as string;
                if (string.IsNullOrEmpty(currentValue))
                {
                    rezeptProp.SetValue(b, def.DefaultRecipeId);
                    DebugLogger.LogServices($"BuildingFactory: Set RezeptIdOverride to '{def.DefaultRecipeId}' for {b.GetType().Name}");
                }
            }
        }

        // Database + weitere Dependencies injizieren
        try
        {
            b.Initialize(this.database, this.dataIndex);
        }
        catch
        {
        }
        try
        {
            b.InitializeDependencies(this.productionManager, this.economyManager, this.eventHub);
        }
        catch
        {
        }
        if (b is City city)
        {
            try
            {
                city.Initialize(this.eventHub, this.gameTimeManager, this.simulation);
            }
            catch
            {
            }
        }

        return b;
    }
}



