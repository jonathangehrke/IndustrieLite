// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Gemeinsame Basisklasse fuer alle Repositories.
/// Verantwortet Loader-Reihenfolge, Caching und generische Abfragen.
/// </summary>
public abstract class BaseRepository<T> : IDataRepository<T>
    where T : Resource
{
    protected readonly Dictionary<string, T> eintraegeNachId = new(StringComparer.Ordinal);
    protected readonly List<IDataLoader<T>> ladeReihenfolge = new();

    public async Task LoadDataAsync(SceneTree sceneTree)
    {
        foreach (var loader in this.ladeReihenfolge.OrderBy(l => l.Priority))
        {
            var datensaetze = await loader.LoadAsync(sceneTree);
            if (datensaetze == null || datensaetze.Count == 0)
            {
                continue;
            }

            this.PopulateCache(datensaetze);
            DebugLogger.LogServices(() => $"{this.GetType().Name}: Daten via {loader.LoaderName} geladen ({this.eintraegeNachId.Count})");
            return;
        }

        this.eintraegeNachId.Clear();
        DebugLogger.LogServices(() => $"{this.GetType().Name}: Keine Datenquellen lieferten Eintraege");
    }

    protected void PopulateCache(IReadOnlyCollection<T> items)
    {
        this.eintraegeNachId.Clear();
        foreach (var item in items)
        {
            var id = this.GetId(item);
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }
            this.eintraegeNachId[id] = item;
        }
        this.NachCacheAktualisierung(items);
    }

    protected virtual void NachCacheAktualisierung(IReadOnlyCollection<T> items)
    {
        // Standard: keine zusaetzliche Verarbeitung noetig.
    }

    public virtual T? GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        if (this.eintraegeNachId.TryGetValue(id, out var eintrag))
        {
            return eintrag;
        }

        return this.ResolveLegacyId(id);
    }

    protected virtual T? ResolveLegacyId(string id) => null;

    public virtual IReadOnlyCollection<T> GetAll() => this.eintraegeNachId.Values.ToList();

    public virtual IReadOnlyCollection<T> GetByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return Array.Empty<T>();
        }

        var ergebnis = new List<T>();
        foreach (var eintrag in this.eintraegeNachId.Values)
        {
            var eintragsKategorie = this.GetKategorie(eintrag);
            if (!string.IsNullOrEmpty(eintragsKategorie) && string.Equals(eintragsKategorie, category, StringComparison.OrdinalIgnoreCase))
            {
                ergebnis.Add(eintrag);
            }
        }
        return ergebnis;
    }

    protected virtual string? GetKategorie(T item) => null;

    public virtual bool Exists(string id) => this.GetById(id) != null;

    public virtual Result<T> TryGet(string id)
    {
        var eintrag = this.GetById(id);
        if (eintrag != null)
        {
            return Result<T>.Success(eintrag);
        }
        return Result<T>.Fail($"Eintrag '{id}' wurde nicht gefunden.");
    }

    public virtual IReadOnlyDictionary<string, T> AsDictionary()
    {
        return new Dictionary<string, T>(this.eintraegeNachId, StringComparer.Ordinal);
    }

    protected abstract string GetId(T item);
}


