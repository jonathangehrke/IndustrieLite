// SPDX-License-Identifier: MIT

/// <summary>
/// Interface for the Production Manager - handles production ticks and producer registration.
/// </summary>
public interface IProductionManager
{
    /// <summary>
    /// Registers a producer with the production system.
    /// </summary>
    void RegisterProducer(IProducer producer);

    /// <summary>
    /// Removes a producer from the production system.
    /// </summary>
    void UnregisterProducer(IProducer producer);

    /// <summary>
    /// Executes a production tick (set capacities, check needs, consume).
    /// </summary>
    void ProcessProductionTick();

    /// <summary>
    /// Sets the tick rate of production in Hertz (0 disables).
    /// </summary>
    void SetProduktionsTickRate(double rate);

    /// <summary>
    /// Clears all production data (lifecycle management).
    /// </summary>
    void ClearAllData();
}
