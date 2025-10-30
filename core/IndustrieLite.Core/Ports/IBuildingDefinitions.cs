// SPDX-License-Identifier: MIT
using IndustrieLite.Core.Domain;

namespace IndustrieLite.Core.Ports;

public interface IBuildingDefinitions
{
    BuildingDefinition? GetById(string id);
}

