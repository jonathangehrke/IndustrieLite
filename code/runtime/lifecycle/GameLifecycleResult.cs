// SPDX-License-Identifier: MIT
using System;

namespace IndustrieLite.Runtime.Lifecycle
{
    public class GameLifecycleResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception? Exception { get; set; }

        public static GameLifecycleResult CreateSuccess()
        {
            return new GameLifecycleResult { Success = true };
        }

        public static GameLifecycleResult CreateError(string message, Exception? exception = null)
        {
            return new GameLifecycleResult
            {
                Success = false,
                ErrorMessage = message,
                Exception = exception
            };
        }
    }
}