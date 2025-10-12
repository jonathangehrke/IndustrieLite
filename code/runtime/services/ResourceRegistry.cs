// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;

/// <summary>
/// Zentrale Registrierung aller Ressourcen-IDs.
/// - Fuehrt dynamische StringName-IDs ein
/// - Arbeitet ausschliesslich mit StringName-IDs (Legacy-Enum entfernt)
/// - Laedt bekannte Ressourcen aus der Database (falls vorhanden)
/// </summary>
public partial class ResourceRegistry : Node
{
    // Interner Speicher der bekannten Ressourcen-IDs
    private readonly HashSet<StringName> _resourceIds = new();

    // Legacy-Mapping entfernt

    // Standard-IDs (Fallback, wenn keine Database vorhanden ist)
    private static readonly StringName IdPower    = new("power");
    private static readonly StringName IdWater    = new("water");
    private static readonly StringName IdWorkers  = new("workers");
    private static readonly StringName IdChickens = new("chickens");
    private static readonly StringName IdEgg      = new("egg");
    private static readonly StringName IdPig      = new("pig");
    private static readonly StringName IdGrain    = new("grain");

    public override void _Ready()
    {
        // Selbstregistrierung im DI-Container
        ServiceContainer.Instance?.RegisterNamedService("ResourceRegistry", this);

        // Standardressourcen sicherstellen
        EnsureDefaultResources();

        // Optional: Ressourcen aus Database uebernehmen (dynamisch)
        TrySeedFromDatabase();
        // Sicherheit: Reihenfolge der _Ready()-Aufrufe kann variieren
        CallDeferred(nameof(TrySeedFromDatabase));

        DebugLogger.LogServices(() => $"ResourceRegistry: initialisiert mit {_resourceIds.Count} Ressourcen-IDs");
    }

    private void EnsureDefaultResources()
    {
        RegisterResource(ResourceIds.PowerName);
        RegisterResource(ResourceIds.WaterName);
        RegisterResource(ResourceIds.WorkersName);
        RegisterResource(ResourceIds.ChickensName);
        RegisterResource(ResourceIds.EggName);
        RegisterResource(ResourceIds.PigName);
        RegisterResource(ResourceIds.GrainName);

        // Kein Enum-Mapping mehr
    }

    private void TrySeedFromDatabase()
    {
        var db = ServiceContainer.Instance?.GetNamedService<Database>("Database");
        if (db == null || db.ResourcesById == null)
        {
            DebugLogger.LogServices("ResourceRegistry: Keine Database gefunden oder keine Ressourcen definiert (verwende Fallbacks)");
            return;
        }

        foreach (var id in db.ResourcesById.Keys)
        {
            RegisterResource(new StringName(id));
        }
    }

    /// <summary>
    /// Registriert eine Ressourcen-ID (idempotent).
    /// </summary>
    public void RegisterResource(StringName id)
    {
        if (_resourceIds.Add(id))
        {
            DebugLogger.LogServices(() => $"ResourceRegistry: Registriert Ressource '{id}'");
        }
    }

    /// <summary>
    /// Prueft, ob eine Ressourcen-ID bekannt ist.
    /// </summary>
    public bool HasResource(StringName id) => _resourceIds.Contains(id);

    /// <summary>
    /// Liefert alle registrierten Ressourcen-IDs (readonly-Kopie).
    /// </summary>
    public IReadOnlyCollection<StringName> GetAllResourceIds() => _resourceIds;

    /// <summary>
    /// GDScript-freundliche Liste aller IDs.
    /// </summary>
    public Godot.Collections.Array<StringName> GetAllResourceIdsForUI()
    {
        var arr = new Godot.Collections.Array<StringName>();
        foreach (var id in _resourceIds)
            arr.Add(id);
        return arr;
    }

    // Legacy-API (Enum) entfernt
}

