// SPDX-License-Identifier: MIT

/// <summary>
/// Adapter von Database (LegacyAdapter) auf IBuildingDefinitionProvider-Port.
/// </summary>
public sealed class DatabaseBuildingDefinitionProvider : IBuildingDefinitionProvider
{
    private readonly Database db;

    public DatabaseBuildingDefinitionProvider(Database db)
    {
        this.db = db;
    }

    public BuildingDef? GetBuilding(string id)
    {
        return this.db.GetBuilding(id);
    }
}

