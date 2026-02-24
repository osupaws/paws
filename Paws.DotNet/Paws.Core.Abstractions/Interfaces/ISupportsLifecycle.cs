using System.Threading.Tasks;

namespace Paws.Core.Abstractions.Interfaces
{
    public interface ISupportsLifecycle
    {
        /// <summary>
        /// Called when the plugin's UI becomes visible/active.
        /// Use this to start heavy rendering loops or increase polling frequency.
        /// </summary>
        Task OnUiWakeAsync();

        /// <summary>
        /// Called when the plugin's UI is hidden or the user navigates away.
        /// Use this to pause heavy rendering or throttle polling to save resources.
        /// </summary>
        Task OnUiSleepAsync();
    }
}
