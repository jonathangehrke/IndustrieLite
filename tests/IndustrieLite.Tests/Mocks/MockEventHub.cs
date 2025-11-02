// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;

namespace IndustrieLite.Tests.Mocks;

/// <summary>
/// Mock implementation of EventHub for testing.
/// Tracks emitted events without full Godot Signal system.
/// </summary>
public class MockEventHub
{
    public List<string> EmittedEvents { get; } = new();
    public bool EmitWasCalled { get; set; }

    public void Emit(string eventName, params object[] args)
    {
        EmittedEvents.Add(eventName);
        EmitWasCalled = true;
    }

    public void Connect(string eventName, Action callback)
    {
        // No-op for mock
    }

    public void ClearEmittedEvents()
    {
        EmittedEvents.Clear();
        EmitWasCalled = false;
    }
}
