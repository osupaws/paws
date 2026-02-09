using System;
using System.Threading.Tasks;
using Realms;
using Paws.Core.Abstractions.Interfaces.Contexts;

namespace Paws.Core.Abstractions.Interfaces.Services
{
    public interface ILazerService
    {
        ILazerContext? GetLazerContext();
        dynamic? GetLazerDatabase();
        Task PerformLazerWriteAsync(Action<Realm> action);
    }
}
