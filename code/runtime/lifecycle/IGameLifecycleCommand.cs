// SPDX-License-Identifier: MIT
namespace IndustrieLite.Runtime.Lifecycle
{
    using System.Threading.Tasks;

    public interface IGameLifecycleCommand
    {
        string Name { get; }

        bool CanExecute(GameLifecycleContext context);

        Task<GameLifecycleResult> ExecuteAsync(GameLifecycleContext context);
    }
}
