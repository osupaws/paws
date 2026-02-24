using System;

namespace Paws.Core.Abstractions.Exceptions
{
    public class LazerIsRunningException : Exception
    {
        public LazerIsRunningException() : base("osu!lazer is currently running and cannot be modified safely.") { }
    }

    public class LazerAccessConflictException : Exception
    {
        public LazerAccessConflictException(string message) : base(message) { }
        public LazerAccessConflictException(string message, Exception innerException) : base(message, innerException) { }
    }
}
