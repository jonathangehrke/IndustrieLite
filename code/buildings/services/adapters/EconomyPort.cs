// SPDX-License-Identifier: MIT

/// <summary>
/// Adapter von EconomyManager auf IEconomy-Port für testbare Kernlogik.
/// </summary>
public sealed class EconomyPort : IEconomy
{
    private readonly EconomyManager inner;

    public EconomyPort(EconomyManager inner)
    {
        this.inner = inner;
    }

    public bool CanAfford(int amount) => this.inner.CanAfford(amount);
}

