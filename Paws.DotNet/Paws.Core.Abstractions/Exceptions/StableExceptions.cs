using System;

namespace Paws.Core.Abstractions.Exceptions
{
    public class StableIsRunningException : Exception
    {
        public StableIsRunningException() : base("osu!stable is currently running and cannot be modified safely.") { }
    }
}
