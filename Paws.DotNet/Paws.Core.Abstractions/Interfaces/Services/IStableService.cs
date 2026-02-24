using System;
using System.Threading.Tasks;
using Paws.Core.Abstractions.Interfaces.Contexts;

namespace Paws.Core.Abstractions.Interfaces.Services
{
    public interface IStableService
    {
        Task<object?> GetStableOsuDbAsync();
        Task<object?> GetStableScoresDbAsync();
        Task PerformStableWriteAsync(Action<string> action);
        IStableContext GetStableContext();
    }
}
