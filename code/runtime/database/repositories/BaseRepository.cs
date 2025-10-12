// SPDX-License-Identifier: MIT
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Gemeinsame Basisklasse fuer alle Repositories.
/// Verantwortet Loader-Reihenfolge, Caching und generische Abfragen.
/// </summary>
public abstract class BaseRepository<T> : IDataRepository<T> where T : Resource
{
    protected readonly Dictionary<string, T> eintraegeNachId = new();
    protected readonly List<IDataLoader<T>> ladeReihenfolge = new();

    public async Task LoadDataAsync(SceneTree sceneTree)
    {
        foreach (var loader in ladeReihenfolge.OrderBy(l => l.Priority))
        {
            var datensaetze = await loader.LoadAsync(sceneTree);
            if (datensaetze == null || datensaetze.Count == 0)
            {
                continue;
            }

            PopulateCache(datensaetze);
            DebugLogger.LogServices(() => $"{GetType().Name}: Daten via {loader.LoaderName} geladen ({eintraegeNachId.Count})");
            return;
        }

        eintraegeNachId.Clear();
        DebugLogger.LogServices(() => $"{GetType().Name}: Keine Datenquellen lieferten Eintraege");
    }

    protected void PopulateCache(IReadOnlyCollection<T> items)
    {
        eintraegeNachId.Clear();
        foreach (var item in items)
        {
            var id = GetId(item);
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }
            eintraegeNachId[id] = item;
        }
        NachCacheAktualisierung(items);
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

        if (eintraegeNachId.TryGetValue(id, out var eintrag))
        {
            return eintrag;
        }

        return ResolveLegacyId(id);
    }

    protected virtual T? ResolveLegacyId(string id) => null;

    public virtual IReadOnlyCollection<T> GetAll() => eintraegeNachId.Values.ToList();

    public virtual IReadOnlyCollection<T> GetByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return Array.Empty<T>();
        }

        var ergebnis = new List<T>();
        foreach (var eintrag in eintraegeNachId.Values)
        {
            var eintragsKategorie = GetKategorie(eintrag);
            if (!string.IsNullOrEmpty(eintragsKategorie) && string.Equals(eintragsKategorie, category, StringComparison.OrdinalIgnoreCase))
            {
                ergebnis.Add(eintrag);
            }
        }
        return ergebnis;
    }

    protected virtual string? GetKategorie(T item) => null;

    public virtual bool Exists(string id) => GetById(id) != null;

    public virtual Result<T> TryGet(string id)
    {
        var eintrag = GetById(id);
        if (eintrag != null)
        {
            return Result<T>.Success(eintrag);
        }
        return Result<T>.Fail($"Eintrag '{id}' wurde nicht gefunden.");
    }

    public virtual IReadOnlyDictionary<string, T> AsDictionary()
    {
        return new Dictionary<string, T>(eintraegeNachId);
    }

    protected abstract string GetId(T item);
}


