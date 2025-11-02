// SPDX-License-Identifier: MIT

/// <summary>
/// Port-Interface: Liefert definitorische Gebäudedaten (Größe, Kosten, etc.).
/// </summary>
public interface IBuildingDefinitionProvider
{
    /// <summary>
    /// Liefert eine BuildingDef zu einer ID oder null, wenn unbekannt.
    /// </summary>
    BuildingDef? GetBuilding(string id);
}

