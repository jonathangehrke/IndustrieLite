// SPDX-License-Identifier: MIT
namespace IndustrieLite.Core.Domain;

public sealed class BuildingDefinition
{
    public string Id { get; }
    public int Width { get; }
    public int Height { get; }
    public int Cost { get; }

    public BuildingDefinition(string id, int width, int height, int cost)
    {
        Id = id;
        Width = width;
        Height = height;
        Cost = cost;
    }
}

