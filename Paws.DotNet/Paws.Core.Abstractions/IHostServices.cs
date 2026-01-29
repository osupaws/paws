using Realms;

namespace Paws.Core.Abstractions;

/// <summary>
/// Defines the contract for services that the Paws host provides to all plugins.
/// This interface is the primary bridge between a plugin and the main application.
/// </summary>
public interface IHostServices
{
    /// <summary>
    /// Logs a message to the main Paws console output, which is visible in the Electron logs.
    /// </summary>
    /// <param name="message">The message content to log.</param>
    /// <param name="level">The severity level of the message.</param>
    /// <param name="pluginName">The name of the plugin originating the log. Filled automatically if null.</param>
    void LogMessage(string message, PawsLogLvl level = PawsLogLvl.Information, string? pluginName = null);

    /// <summary>
    /// Logs a structured progress event (0-100%) to the console.
    /// The frontend parses these logs to update the Splash Screen.
    /// </summary>
    /// <param name="message">Description of the current operation.</param>
    /// <param name="percent">Progress percentage (0.0 to 100.0).</param>
    void LogProgress(string message, double percent);

    /// <summary>
    /// Gets a disposable context for accessing osu!lazer data in a decoupled, strongly-typed manner.
    /// The returned context owns the Realm instance connection, so you MUST dispose it (using statement).
    /// </summary>
    /// <returns>A LazerContext, or null if database is inaccessible.</returns>
    LazerContext? GetLazerContext();

    /// <summary>
    /// Gets a read-only, dynamic Realm instance for the osu!lazer database.
    /// The connection is managed by the host. The plugin should use and dispose of this instance promptly.
    /// Returns null if the path to lazer is not set or the database is inaccessible.
    /// </summary>
    /// <returns>A dynamic <see cref="Realm"/> instance for read operations, or null.</returns>
    dynamic? GetLazerDatabase();

    /// <summary>
    /// Safely performs a write operation on the osu!lazer database.
    /// This method guarantees that the osu!lazer process is not running before attempting the operation.
    /// It handles the database connection, transaction, and resource disposal.
    /// </summary>
    /// <param name="action">The action to perform. It receives a writeable, dynamic Realm instance.</param>
    /// <exception cref="LazerIsRunningException">Thrown if the osu!lazer process is detected.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the database path isn't set or another error occurs.</exception>
    Task PerformLazerWriteAsync(Action<Realm> action);

    /// <summary>
    /// Asynchronously gets the parsed osu!stable `osu!.db` file.
    /// The host caches the result for performance and invalidates it if the file changes.
    /// </summary>
    /// <returns>A parsed OsuDatabase object (from OsuParsers), or null if not found/parsable.</returns>
    Task<object?> GetStableOsuDbAsync();

    /// <summary>
    /// Asynchronously gets the parsed osu!stable `scores.db` file.
    /// The host caches the result for performance and invalidates it if the file changes.
    /// </summary>
    /// <returns>A parsed ScoresDatabase object (from OsuParsers), or null if not found/parsable.</returns>
    Task<object?> GetStableScoresDbAsync();

    /// <summary>
    /// Safely performs an action that may modify osu!stable files.
    /// This method guarantees that the osu!stable process is not running before executing the action.
    /// </summary>
    /// <param name="action">The action to perform. It receives the validated root path of the stable installation.</param>
    /// <exception cref="StableIsRunningException">Thrown if the osu!stable process is detected.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the stable path isn't set.</exception>
    Task PerformStableWriteAsync(Action<string> action);
}
