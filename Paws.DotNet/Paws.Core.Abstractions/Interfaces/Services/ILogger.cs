using Paws.Core.Abstractions.Enums;

namespace Paws.Core.Abstractions.Interfaces.Services
{
    public interface ILogger
    {
        void LogMessage(string message, PawsLogLvl level = PawsLogLvl.Information, string? pluginName = null);
        void LogProgress(string message, double percent);
    }
}
