// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using Godot;

/// <summary>
/// Interface für Einheiten mit dynamischem Inventar/Bestand pro Ressource.
/// Ermöglicht stringbasierte (StringName) Ressourcen-IDs und generische Verarbeitung
/// von Lagerbeständen ohne harte Kopplung an Enums.
/// </summary>
public interface IHasInventory
{
    /// <summary>
    /// Liefert eine schreibgeschützte Sicht auf das Inventar.
    /// Schlüssel: Ressourcen-ID (StringName), Wert: Menge (float).
    /// </summary>
    /// <returns></returns>
    IReadOnlyDictionary<StringName, float> GetInventory();

    /// <summary>
    /// Setzt die absolute Menge für die angegebene Ressource.
    /// </summary>
    /// <param name="resourceId">Ressourcen-ID als StringName.</param>
    /// <param name="amount">Neue Menge (>= 0 empfohlen).</param>
    void SetInventoryAmount(StringName resourceId, float amount);

    /// <summary>
    /// Erhöht die Menge der angegebenen Ressource um den Wert.
    /// Negativwerte sind erlaubt, werden aber nicht empfohlen.
    /// </summary>
    /// <param name="resourceId">Ressourcen-ID als StringName.</param>
    /// <param name="amount">Delta-Menge (kann negativ sein).</param>
    void AddToInventory(StringName resourceId, float amount);

    /// <summary>
    /// Versucht, die angeforderte Menge der Ressource zu entnehmen.
    /// Gibt true zurück, wenn genügend Bestand vorhanden war und abgebucht wurde.
    /// </summary>
    /// <param name="resourceId">Ressourcen-ID als StringName.</param>
    /// <param name="amount">Angeforderte Menge (>= 0).</param>
    /// <returns>true, wenn erfolgreich entnommen; sonst false.</returns>
    bool ConsumeFromInventory(StringName resourceId, float amount);
}

