// SPDX-License-Identifier: MIT
using IndustrieLite.Core.Ports;
using IndustrieLite.Core.Util;

namespace IndustrieLite.Core.Economy;

/// <summary>
/// Engine-freier Economy-Service: Geldbestand, Debit/Credit, CanAfford.
/// </summary>
public sealed class EconomyCoreService : IEconomyCore, IEconomyOps
{
    private double money;
    private readonly IEconomyEvents? events;

    public EconomyCoreService(double startingMoney = 0.0, IEconomyEvents? events = null)
    {
        this.money = startingMoney;
        this.events = events;
    }

    public double GetMoney() => this.money;

    public bool CanAfford(int amount) => amount <= 0 ? true : this.money >= amount;

    public bool CanAfford(double amount) => amount <= 0.0 ? true : this.money >= amount;

    public CoreResult TryDebit(double amount)
    {
        if (amount <= 0.0)
        {
            return CoreResult.Fail("economy.invalid_amount", "Betrag muss positiv sein");
        }
        if (this.money + 1e-6 < amount)
        {
            return CoreResult.Fail("economy.insufficient_funds", "Unzureichende Mittel");
        }

        this.money -= amount;
        this.events?.OnMoneyChanged(this.money);
        return CoreResult.Success();
    }

    public CoreResult TryCredit(double amount)
    {
        if (amount <= 0.0)
        {
            return CoreResult.Fail("economy.invalid_amount", "Betrag muss positiv sein");
        }

        this.money += amount;
        this.events?.OnMoneyChanged(this.money);
        return CoreResult.Success();
    }

    public void SetMoney(double amount)
    {
        this.money = amount;
        this.events?.OnMoneyChanged(this.money);
    }
}
