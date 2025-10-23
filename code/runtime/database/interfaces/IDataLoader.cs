// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

/// <summary>
/// Abstraktes Ladeobjekt fuer definierte Datenquellen.
/// Liefert eine Menge an Ressourcen oder eine leere Liste, wenn keine Daten vorhanden sind.
/// </summary>
public interface IDataLoader<T>
    where T : Resource
{
    Task<IReadOnlyCollection<T>> LoadAsync(SceneTree sceneTree);

    string LoaderName { get; }

    int Priority { get; }
}

