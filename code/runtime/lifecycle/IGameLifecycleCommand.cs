// SPDX-License-Identifier: MIT
using System.Threading.Tasks;

namespace IndustrieLite.Runtime.Lifecycle
{
    public interface IGameLifecycleCommand
    {
        string Name { get; }
        bool CanExecute(GameLifecycleContext context);
        Task<GameLifecycleResult> ExecuteAsync(GameLifecycleContext context);
    }
}