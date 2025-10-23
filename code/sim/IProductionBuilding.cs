// SPDX-License-Identifier: MIT
using Godot.Collections;

/// <summary>
/// Interface fuer Produktionsgebaeude mit Rezept-Steuerung (Phase 1).
/// Stellt UI-relevante Methoden bereit, bleibt komplementaer zu IProducer.
/// </summary>
public interface IProductionBuilding
{
    string GetRecipeIdForUI();

    bool SetRecipeFromUI(string rezeptId);

    Dictionary GetProductionForUI();

    Dictionary GetNeedsForUI();
}

