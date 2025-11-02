// SPDX-License-Identifier: MIT
using IndustrieLite.Core.Primitives;

namespace IndustrieLite.Core.Ports;

public interface ILandGrid
{
    bool IsOwned(Int2 cell);
    int GetWidth();
    int GetHeight();
}

