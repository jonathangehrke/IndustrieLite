// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using IndustrieLite.Core.Domain;

namespace IndustrieLite.Core.Ports;

/// <summary>
/// Port für Ressourcenevents (enginefrei). Ein Adapter kann auf UI/EventHub mappen.
/// </summary>
public interface IResourceEvents
{
    void OnResourceInfoChanged(IReadOnlyDictionary<string, ResourceInfo> snapshot);
}

