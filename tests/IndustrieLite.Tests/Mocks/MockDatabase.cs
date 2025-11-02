// SPDX-License-Identifier: MIT
using System.Collections.Generic;

namespace IndustrieLite.Tests.Mocks;

/// <summary>
/// Mock implementation of Database for testing.
/// Provides in-memory storage without SQLite dependency.
/// </summary>
public class MockDatabase
{
    private readonly Dictionary<string, object> data = new();

    public void Set(string key, object value)
    {
        data[key] = value;
    }

    public T? Get<T>(string key) where T : class
    {
        if (data.TryGetValue(key, out var value))
        {
            return value as T;
        }
        return null;
    }

    public void Clear()
    {
        data.Clear();
    }
}
