// SPDX-License-Identifier: MIT
using System.Collections.Generic;

namespace IndustrieLite.Tests.Mocks;

/// <summary>
/// Mock implementation of GameDatabase for testing.
/// Provides in-memory game state without persistence.
/// </summary>
public class MockGameDatabase
{
    public bool IsReady { get; set; } = true;
    public Dictionary<string, object> GameState { get; } = new();

    public void SaveGameState(string key, object value)
    {
        GameState[key] = value;
    }

    public T? LoadGameState<T>(string key) where T : class
    {
        if (GameState.TryGetValue(key, out var value))
        {
            return value as T;
        }
        return null;
    }

    public void Clear()
    {
        GameState.Clear();
    }
}
