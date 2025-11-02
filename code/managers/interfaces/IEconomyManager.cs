// SPDX-License-Identifier: MIT
using System;

/// <summary>
/// Interface for the Economy Manager - handles money and transactions.
/// </summary>
public interface IEconomyManager
{
    /// <summary>
    /// Gets the current money balance.
    /// </summary>
    double Money { get; }

    /// <summary>
    /// Adjusts the money balance by the specified amount.
    /// Positive amounts are credited, negative amounts are debited.
    /// </summary>
    void AddMoney(double amount);

    /// <summary>
    /// Checks if the current money balance can cover the specified cost.
    /// </summary>
    bool CanAfford(double cost);

    /// <summary>
    /// Result-based variant of CanAfford with validation and structured logging.
    /// </summary>
    Result<bool> CanAffordEx(double cost, string? correlationId = null);

    /// <summary>
    /// Deducts the cost if possible.
    /// </summary>
    bool SpendMoney(double cost);

    /// <summary>
    /// Deducts cost using Result pattern, validates inputs and logs structurally.
    /// </summary>
    Result TryDebit(double cost, string? correlationId = null);

    /// <summary>
    /// Credits amount using Result pattern, validates inputs and logs structurally.
    /// </summary>
    Result TryCredit(double amount, string? correlationId = null);

    /// <summary>
    /// Returns the current money balance.
    /// </summary>
    double GetMoney();

    /// <summary>
    /// Sets the money balance absolutely (e.g., for loading or starting a game).
    /// </summary>
    void SetMoney(double amount);

    /// <summary>
    /// Clears all economy data (lifecycle management).
    /// </summary>
    void ClearAllData();

    /// <summary>
    /// Sets the starting capital (lifecycle management).
    /// </summary>
    void SetStartingMoney(double amount);
}
