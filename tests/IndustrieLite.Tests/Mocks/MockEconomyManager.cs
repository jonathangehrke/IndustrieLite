// SPDX-License-Identifier: MIT
using System;

namespace IndustrieLite.Tests.Mocks;

/// <summary>
/// Mock implementation of IEconomyManager for testing.
/// Provides minimal implementation with public state for assertions.
/// </summary>
public class MockEconomyManager : IEconomyManager
{
    public double Money { get; set; } = 1000.0;
    public bool SpendMoneyWasCalled { get; set; }
    public bool AddMoneyWasCalled { get; set; }
    public bool ClearAllDataWasCalled { get; set; }

    public void AddMoney(double amount)
    {
        Money += amount;
        AddMoneyWasCalled = true;
    }

    public bool CanAfford(double cost)
    {
        return Money >= cost;
    }

    public Result<bool> CanAffordEx(double cost, string? correlationId = null)
    {
        return Result<bool>.Ok(Money >= cost);
    }

    public bool SpendMoney(double cost)
    {
        if (Money >= cost)
        {
            Money -= cost;
            SpendMoneyWasCalled = true;
            return true;
        }
        return false;
    }

    public Result TryDebit(double cost, string? correlationId = null)
    {
        if (Money >= cost)
        {
            Money -= cost;
            return Result.Ok();
        }
        return Result.Err("Insufficient funds");
    }

    public Result TryCredit(double amount, string? correlationId = null)
    {
        Money += amount;
        return Result.Ok();
    }

    public double GetMoney() => Money;

    public void SetMoney(double amount)
    {
        Money = amount;
    }

    public void ClearAllData()
    {
        Money = 0;
        ClearAllDataWasCalled = true;
    }

    public void SetStartingMoney(double amount)
    {
        Money = amount;
    }
}
