using System;
using Paws.Core.Abstractions.Models;

namespace Paws.Host.Services.Lazer
{
    /// <summary>
    /// Section: File System
    /// Handles mappings for: RealmFile, RealmNamedFileUsage
    /// </summary>
    public static class LazerFileMapper
    {
        public static LazerFile MapToDto(dynamic fileUsage)
        {
            // Realm: RealmNamedFileUsage.Filename / .File.Hash
            return new LazerFile
            {
                Filename = fileUsage.Filename,
                Hash = fileUsage.File.Hash
            };
        }
    }
}
