// SPDX-License-Identifier: MIT

/// <summary>
/// Minimales Interface fuer Geld-Abfragen (Anti-Zyklen).
/// </summary>
public interface IEconomyQuery
{
    bool CanAfford(double cost);
}

