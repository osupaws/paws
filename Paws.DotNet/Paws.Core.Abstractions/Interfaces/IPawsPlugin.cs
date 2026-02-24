using System.Threading.Tasks;
using Paws.Core.Abstractions.Interfaces.Services;

namespace Paws.Core.Abstractions.Interfaces
{
    public interface IPawsPlugin : IPlugin
    {
        string IconName { get; }
        Task Initialize(IHost host);
        Task<object?> ExecuteCommandAsync(string commandName, object? payload);
    }
}
