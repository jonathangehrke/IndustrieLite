// SPDX-License-Identifier: MIT
using IndustrieLite.Core.Primitives;

namespace IndustrieLite.Core.Placement;

public sealed class BuildingSpec
{
    public string DefinitionId { get; }
    public Int2 GridPos { get; }
    public Int2 Size { get; }
    public int TileSize { get; }

    public BuildingSpec(string definitionId, Int2 gridPos, Int2 size, int tileSize)
    {
        DefinitionId = definitionId;
        GridPos = gridPos;
        Size = size;
        TileSize = tileSize;
    }
}

