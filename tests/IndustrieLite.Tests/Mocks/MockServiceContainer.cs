// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;

namespace IndustrieLite.Tests.Mocks;

/// <summary>
/// Mock implementation of ServiceContainer for testing DIContainer.
/// Provides named service registration without full Godot autoload system.
/// </summary>
public class MockServiceContainer
{
    private readonly Dictionary<string, object> services = new();

    public void RegisterNamedService(string name, object service)
    {
        services[name] = service;
    }

    public T? GetNamedService<T>(string name) where T : class
    {
        if (services.TryGetValue(name, out var service))
        {
            return service as T;
        }
        return null;
    }

    public void RegisterInterface<T>(object implementation) where T : class
    {
        services[typeof(T).Name] = implementation;
    }

    public T? GetService<T>() where T : class
    {
        return GetNamedService<T>(typeof(T).Name);
    }
}
