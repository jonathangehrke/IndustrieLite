// SPDX-License-Identifier: MIT
using IndustrieLite.Core.Economy;
using IndustrieLite.Core.Ports;
using Xunit;

public class EconomyCoreServiceTests
{
    private sealed class Sink : IEconomyEvents
    {
        public double LastMoney { get; private set; }
        public int Count { get; private set; }
        public void OnMoneyChanged(double money) { LastMoney = money; Count++; }
    }

    [Fact]
    public void Debit_Succeeds_WhenEnoughMoney()
    {
        var sink = new Sink();
        var eco = new EconomyCoreService(100.0, sink);
        var res = eco.TryDebit(40.0);
        Assert.True(res.Ok);
        Assert.Equal(60.0, eco.GetMoney(), 3);
        Assert.Equal(1, sink.Count);
        Assert.Equal(60.0, sink.LastMoney, 3);
    }

    [Fact]
    public void Debit_Fails_WhenInsufficient()
    {
        var eco = new EconomyCoreService(10.0);
        var res = eco.TryDebit(25.0);
        Assert.False(res.Ok);
        Assert.Equal("economy.insufficient_funds", res.Error!.Code);
        Assert.Equal(10.0, eco.GetMoney(), 3);
    }

    [Fact]
    public void Credit_Increases_Money_And_FiresEvent()
    {
        var sink = new Sink();
        var eco = new EconomyCoreService(5.0, sink);
        var res = eco.TryCredit(7.5);
        Assert.True(res.Ok);
        Assert.Equal(12.5, eco.GetMoney(), 3);
        Assert.Equal(1, sink.Count);
        Assert.Equal(12.5, sink.LastMoney, 3);
    }

    [Fact]
    public void InvalidAmounts_Fail()
    {
        var eco = new EconomyCoreService(0.0);
        var d1 = eco.TryDebit(0);
        var d2 = eco.TryDebit(-1);
        var c1 = eco.TryCredit(0);
        var c2 = eco.TryCredit(-2.5);
        Assert.False(d1.Ok); Assert.False(d2.Ok);
        Assert.False(c1.Ok); Assert.False(c2.Ok);
        Assert.Equal("economy.invalid_amount", d1.Error!.Code);
        Assert.Equal("economy.invalid_amount", c1.Error!.Code);
    }
}

