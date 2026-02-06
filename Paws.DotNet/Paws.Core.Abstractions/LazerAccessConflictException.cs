using System;

namespace Paws.Core.Abstractions;

/// <summary>
/// Thrown when attempting to open a Writeable connection to the Lazer database while a Read-Only connection is still open in the same process.
/// Plugin developers must Dispose their LazerContext (Read) before calling PerformLazerWriteAsync.
/// </summary>
public class LazerAccessConflictException : InvalidOperationException
{
    public LazerAccessConflictException(string message) : base(message)
    {
    }

    public LazerAccessConflictException(string message, Exception inner) : base(message, inner)
    {
    }
}
