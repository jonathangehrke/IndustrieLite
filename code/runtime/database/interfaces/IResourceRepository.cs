// SPDX-License-Identifier: MIT
using System.Collections.Generic;

/// <summary>
/// Zugriff auf Ressourcen-Definitionen inklusive kategoriebasierter Filter.
/// </summary>
public interface IResourceRepository : IDataRepository<GameResourceDef>
{
    IReadOnlyCollection<GameResourceDef> GetByType(string type);
}

