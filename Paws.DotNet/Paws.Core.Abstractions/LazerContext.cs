using Realms;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Paws.Core.Abstractions;

/// <summary>
/// A disposable context for accessing osu!lazer data in a decoupled, strongly-typed manner.
/// usage: using var context = host.GetLazerContext();
/// </summary>
    // This class is deprecated and has been replaced by ILazerContext.
    // The implementation now resides in Paws.Host.
    public class LazerContext
    {
    }
