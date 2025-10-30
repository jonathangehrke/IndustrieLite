// SPDX-License-Identifier: MIT
using Xunit;
using IndustrieLite.Tests.Mocks;

namespace IndustrieLite.Tests;

/// <summary>
/// Unit tests for EconomyManager - verifies dependency injection and interface contract.
/// Tests IEconomyManager interface compliance and business logic.
/// </summary>
public class EconomyManagerTests
{
    /// <summary>
    /// Test: IEconomyManager interface - AddMoney() increases balance.
    /// </summary>
    [Fact]
    public void AddMoney_IncreasesBalance_ByAmount()
    {
        // Arrange
        var economy = new MockEconomyManager();
        economy.SetMoney(100.0);

        // Act
        economy.AddMoney(50.0);

        // Assert
        Assert.Equal(150.0, economy.Money);
        Assert.True(economy.AddMoneyWasCalled);
    }

    /// <summary>
    /// Test: IEconomyManager interface - SpendMoney() decreases balance when affordable.
    /// </summary>
    [Fact]
    public void SpendMoney_DecreasesBalance_WhenAffordable()
    {
        // Arrange
        var economy = new MockEconomyManager();
        economy.SetMoney(100.0);

        // Act
        var result = economy.SpendMoney(30.0);

        // Assert
        Assert.True(result);
        Assert.Equal(70.0, economy.Money);
        Assert.True(economy.SpendMoneyWasCalled);
    }

    /// <summary>
    /// Test: IEconomyManager interface - SpendMoney() returns false when insufficient funds.
    /// </summary>
    [Fact]
    public void SpendMoney_ReturnsFalse_WhenInsufficientFunds()
    {
        // Arrange
        var economy = new MockEconomyManager();
        economy.SetMoney(50.0);

        // Act
        var result = economy.SpendMoney(100.0);

        // Assert
        Assert.False(result);
        Assert.Equal(50.0, economy.Money); // Balance unchanged
    }

    /// <summary>
    /// Test: IEconomyManager interface - CanAfford() returns correct result.
    /// </summary>
    [Fact]
    public void CanAfford_ReturnsTrue_WhenSufficientFunds()
    {
        // Arrange
        var economy = new MockEconomyManager();
        economy.SetMoney(100.0);

        // Act
        var result = economy.CanAfford(50.0);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanAfford_ReturnsFalse_WhenInsufficientFunds()
    {
        // Arrange
        var economy = new MockEconomyManager();
        economy.SetMoney(50.0);

        // Act
        var result = economy.CanAfford(100.0);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Test: Result pattern - TryDebit() succeeds when affordable.
    /// </summary>
    [Fact]
    public void TryDebit_ReturnsOk_WhenAffordable()
    {
        // Arrange
        var economy = new MockEconomyManager();
        economy.SetMoney(100.0);

        // Act
        var result = economy.TryDebit(30.0);

        // Assert
        Assert.True(result.IsOk);
        Assert.Equal(70.0, economy.Money);
    }

    /// <summary>
    /// Test: Result pattern - TryDebit() returns error when insufficient funds.
    /// </summary>
    [Fact]
    public void TryDebit_ReturnsError_WhenInsufficientFunds()
    {
        // Arrange
        var economy = new MockEconomyManager();
        economy.SetMoney(50.0);

        // Act
        var result = economy.TryDebit(100.0);

        // Assert
        Assert.True(result.IsErr);
        Assert.Contains("Insufficient funds", result.Error ?? "");
        Assert.Equal(50.0, economy.Money); // Balance unchanged
    }

    /// <summary>
    /// Test: Result pattern - TryCredit() always succeeds.
    /// </summary>
    [Fact]
    public void TryCredit_ReturnsOk_Always()
    {
        // Arrange
        var economy = new MockEconomyManager();
        economy.SetMoney(100.0);

        // Act
        var result = economy.TryCredit(50.0);

        // Assert
        Assert.True(result.IsOk);
        Assert.Equal(150.0, economy.Money);
    }

    /// <summary>
    /// Test: Lifecycle - ClearAllData() resets state.
    /// </summary>
    [Fact]
    public void ClearAllData_ResetsBalance_ToZero()
    {
        // Arrange
        var economy = new MockEconomyManager();
        economy.SetMoney(1000.0);

        // Act
        economy.ClearAllData();

        // Assert
        Assert.Equal(0.0, economy.Money);
        Assert.True(economy.ClearAllDataWasCalled);
    }

    /// <summary>
    /// Test: Initialization - SetStartingMoney() sets initial balance.
    /// </summary>
    [Fact]
    public void SetStartingMoney_SetsInitialBalance()
    {
        // Arrange
        var economy = new MockEconomyManager();

        // Act
        economy.SetStartingMoney(5000.0);

        // Assert
        Assert.Equal(5000.0, economy.Money);
    }

    /// <summary>
    /// Test: Edge case - SpendMoney() handles exact balance.
    /// </summary>
    [Fact]
    public void SpendMoney_HandlesExactBalance_Correctly()
    {
        // Arrange
        var economy = new MockEconomyManager();
        economy.SetMoney(100.0);

        // Act
        var result = economy.SpendMoney(100.0);

        // Assert
        Assert.True(result);
        Assert.Equal(0.0, economy.Money);
    }

    /// <summary>
    /// Test: Edge case - Negative amounts (defensive programming).
    /// </summary>
    [Fact]
    public void AddMoney_HandlesNegativeAmounts()
    {
        // Arrange
        var economy = new MockEconomyManager();
        economy.SetMoney(100.0);

        // Act
        economy.AddMoney(-30.0);

        // Assert
        Assert.Equal(70.0, economy.Money); // Negative amount subtracts
    }
}
