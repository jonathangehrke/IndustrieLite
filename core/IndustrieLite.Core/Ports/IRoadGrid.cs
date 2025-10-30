// SPDX-License-Identifier: MIT
using IndustrieLite.Core.Primitives;

namespace IndustrieLite.Core.Ports;

public interface IRoadGrid
{
    bool IsRoad(Int2 cell);
}

