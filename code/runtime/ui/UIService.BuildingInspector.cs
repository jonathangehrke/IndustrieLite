// SPDX-License-Identifier: MIT
using Godot;

public partial class UIService
{
    /// <summary>
    /// Liefert Inspektor-Daten eines Gebaeudes fuer die UI.
    /// </summary>
    public Godot.Collections.Dictionary GetBuildingInspectorData(Node building)
    {
        if (building == null)
        {
            return new Godot.Collections.Dictionary();
        }

        if (building.HasMethod("GetInspectorData"))
        {
            var result = building.Call("GetInspectorData");
            return result.AsGodotDictionary();
        }

        return new Godot.Collections.Dictionary();
    }

    /// <summary>
    /// Liefert Ressourcen-Bedarf eines Gebaeudes fuer die UI.
    /// </summary>
    public Godot.Collections.Dictionary GetBuildingNeeds(Node building)
    {
        if (building == null)
        {
            return new Godot.Collections.Dictionary();
        }

        if (building.HasMethod("GetNeedsForUI"))
        {
            var result = building.Call("GetNeedsForUI");
            return result.AsGodotDictionary();
        }

        return new Godot.Collections.Dictionary();
    }

    /// <summary>
    /// Liefert Produktionsdaten eines Gebaeudes fuer die UI.
    /// </summary>
    public Godot.Collections.Dictionary GetBuildingProduction(Node building)
    {
        if (building == null)
        {
            return new Godot.Collections.Dictionary();
        }

        if (building.HasMethod("GetProductionForUI"))
        {
            var result = building.Call("GetProductionForUI");
            return result.AsGodotDictionary();
        }

        return new Godot.Collections.Dictionary();
    }

    /// <summary>
    /// Liefert Inventar-Daten eines Gebaeudes fuer die UI.
    /// </summary>
    public Godot.Collections.Dictionary GetBuildingInventory(Node building)
    {
        if (building == null)
        {
            return new Godot.Collections.Dictionary();
        }

        if (building.HasMethod("GetInventoryForUI"))
        {
            var result = building.Call("GetInventoryForUI");
            return result.AsGodotDictionary();
        }

        return new Godot.Collections.Dictionary();
    }

    /// <summary>
    /// Liefert die Groesse eines Gebaeudes (in Zellen) fuer die UI.
    /// </summary>
    public Vector2 GetBuildingSize(Node building)
    {
        if (building == null)
        {
            return Vector2.Zero;
        }

        if (building.HasMethod("GetSizeForUI"))
        {
            var result = building.Call("GetSizeForUI");
            return result.AsVector2();
        }

        return Vector2.Zero;
    }

    /// <summary>
    /// Liefert die Raster-Position eines Gebaeudes fuer die UI.
    /// </summary>
    public Vector2I GetBuildingPosition(Node building)
    {
        if (building == null)
        {
            return Vector2I.Zero;
        }

        if (building.HasMethod("GetPosForUI"))
        {
            var result = building.Call("GetPosForUI");
            return result.AsVector2I();
        }

        return Vector2I.Zero;
    }
}
