// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

/// <summary>
/// Zentrale Registrierung aller Ressourcen-IDs.
/// - Fuehrt dynamische StringName-IDs ein
/// - Arbeitet ausschliesslich mit StringName-IDs (Legacy-Enum entfernt)
/// - Laedt bekannte Ressourcen aus der Database (falls vorhanden).
/// </summary>
public partial class ResourceRegistry : Node
{
    // Interner Speicher der bekannten Ressourcen-IDs
    private readonly HashSet<StringName> resourceIds = new();
    private Database? database;

    // Legacy-Mapping entfernt

    // Standard-IDs (Fallback, wenn keine Database vorhanden ist)
    private static readonly StringName IdPower = new("power");
    private static readonly StringName IdWater = new("water");
    private static readonly StringName IdWorkers = new("workers");
    private static readonly StringName IdChickens = new("chickens");
    private static readonly StringName IdEgg = new("egg");
    private static readonly StringName IdPig = new("pig");
    private static readonly StringName IdGrain = new("grain");

    /// <summary>
    /// Initialize with Database dependency (optional).
    /// </summary>
    public void Initialize(Database? database)
    {
        this.database = database;
        this.TrySeedFromDatabase();
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        // Selbstregistrierung im DI-Container
        ServiceContainer.Instance?.RegisterNamedService("ResourceRegistry", this);

        // Standardressourcen sicherstellen
        this.EnsureDefaultResources();

        // Optional: Ressourcen aus Database uebernehmen (dynamisch)
        this.TrySeedFromDatabase();
        // Sicherheit: Reihenfolge der _Ready()-Aufrufe kann variieren
        this.CallDeferred(nameof(this.TrySeedFromDatabase));

        DebugLogger.LogServices(() => $"ResourceRegistry: initialisiert mit {this.resourceIds.Count} Ressourcen-IDs");
    }

    private void EnsureDefaultResources()
    {
        this.RegisterResource(ResourceIds.PowerName);
        this.RegisterResource(ResourceIds.WaterName);
        this.RegisterResource(ResourceIds.WorkersName);
        this.RegisterResource(ResourceIds.ChickensName);
        this.RegisterResource(ResourceIds.EggName);
        this.RegisterResource(ResourceIds.PigName);
        this.RegisterResource(ResourceIds.GrainName);

        // Kein Enum-Mapping mehr
    }

    private void TrySeedFromDatabase()
    {
        if (this.database == null || this.database.ResourcesById == null)
        {
            DebugLogger.LogServices("ResourceRegistry: Keine Database gefunden oder keine Ressourcen definiert (verwende Fallbacks)");
            return;
        }

        foreach (var id in this.database.ResourcesById.Keys)
        {
            this.RegisterResource(new StringName(id));
        }
    }

    /// <summary>
    /// Registriert eine Ressourcen-ID (idempotent).
    /// </summary>
    public void RegisterResource(StringName id)
    {
        if (this.resourceIds.Add(id))
        {
            DebugLogger.LogServices(() => $"ResourceRegistry: Registriert Ressource '{id}'");
        }
    }

    /// <summary>
    /// Prueft, ob eine Ressourcen-ID bekannt ist.
    /// </summary>
    /// <returns></returns>
    public bool HasResource(StringName id) => this.resourceIds.Contains(id);

    /// <summary>
    /// Liefert alle registrierten Ressourcen-IDs (readonly-Kopie).
    /// </summary>
    /// <returns></returns>
    public IReadOnlyCollection<StringName> GetAllResourceIds() => this.resourceIds;

    /// <summary>
    /// GDScript-freundliche Liste aller IDs.
    /// </summary>
    /// <returns></returns>
    public Godot.Collections.Array<StringName> GetAllResourceIdsForUI()
    {
        var arr = new Godot.Collections.Array<StringName>();
        foreach (var id in this.resourceIds)
        {
            arr.Add(id);
        }

        return arr;
    }

    // Legacy-API (Enum) entfernt
}

