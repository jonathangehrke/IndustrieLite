// SPDX-License-Identifier: MIT

namespace IndustrieLite.Tests.Mocks;

/// <summary>
/// Mock implementation of IProductionManager for testing.
/// Provides minimal implementation with public state for assertions.
/// </summary>
public class MockProductionManager : IProductionManager
{
    public bool RegisterProducerWasCalled { get; set; }
    public bool UnregisterProducerWasCalled { get; set; }
    public bool ProcessProductionTickWasCalled { get; set; }
    public bool ClearAllDataWasCalled { get; set; }
    public int RegisteredProducerCount { get; set; }

    public void RegisterProducer(IProducer producer)
    {
        RegisterProducerWasCalled = true;
        RegisteredProducerCount++;
    }

    public void UnregisterProducer(IProducer producer)
    {
        UnregisterProducerWasCalled = true;
        RegisteredProducerCount--;
    }

    public void ProcessProductionTick()
    {
        ProcessProductionTickWasCalled = true;
    }

    public void SetProduktionsTickRate(double rate)
    {
    }

    public void ClearAllData()
    {
        RegisteredProducerCount = 0;
        ClearAllDataWasCalled = true;
    }
}
