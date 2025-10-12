// SPDX-License-Identifier: MIT
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Basis-Interface fuer alle datengetriebenen Repositories.
/// Stellt Zugriff auf geladene Ressourcen bereit und verantwortet das Laden aus Quellenketten.
/// </summary>
public interface IDataRepository<T> where T : Resource
{
    Task LoadDataAsync(SceneTree sceneTree);
    T? GetById(string id);
    IReadOnlyCollection<T> GetAll();
    IReadOnlyCollection<T> GetByCategory(string category);
    bool Exists(string id);
    Result<T> TryGet(string id);
}

