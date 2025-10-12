// SPDX-License-Identifier: MIT
using Godot;

/// <summary>
/// Definiert den Lifecycle-Scope eines Services/Managers.
/// </summary>
public enum ServiceLifecycle
{
    /// <summary>
    /// Singleton (Autoload) - überlebt Scene-Wechsel und Game-Sessions.
    /// Beispiele: EventHub, Database, UIService
    /// </summary>
    Singleton,

    /// <summary>
    /// Session-Scoped (Game-Manager) - wird bei NewGame/LoadGame/MainMenu gelöscht.
    /// Beispiele: EconomyManager, BuildingManager, ResourceManager
    /// </summary>
    Session,

    /// <summary>
    /// Transient (Runtime-Objekte) - niemals im ServiceContainer registriert.
    /// Beispiele: Truck, DeliveryOrder, Buildings
    /// </summary>
    Transient
}

/// <summary>
/// Interface für Services/Managers, die ihren Lifecycle-Scope deklarieren.
/// Ermöglicht automatisches Lifecycle-Management im ServiceContainer.
/// </summary>
public interface ILifecycleScope
{
    /// <summary>
    /// Gibt den Lifecycle-Scope dieses Services zurück.
    /// </summary>
    ServiceLifecycle Lifecycle { get; }
}
