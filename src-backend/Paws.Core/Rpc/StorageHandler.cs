using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Paws.Abstractions.Services;

namespace Paws.Core.Rpc;

/// <summary>
/// Handles filesystem RPC commands (sandbox access, file CRUD).
/// Implements protocol resolution (game://, paws://).
/// </summary>
public class StorageHandler : IRpcHandler
{
    private readonly IStorageService _storage;
    private readonly IVfsService _vfs;

    public StorageHandler(IStorageService storage, IVfsService vfs)
    {
        _storage = storage;
        _vfs = vfs;
    }

    public bool CanHandle(string action) => action.StartsWith("fs") || action.StartsWith("storage");

    public async Task<object?> HandleAsync(string action, string callerId, Dictionary<string, JsonElement> parameters)
    {
        switch (action)
        {
            case "fsRead":
                if (parameters.TryGetValue("path", out var readPathEl))
                {
                    var path = readPathEl.GetString() ?? "";
                    var absPath = ResolvePath(callerId, path);
                    
                    if (!_storage.ValidateAccess(callerId, absPath, false))
                        throw new UnauthorizedAccessException("Read access denied");
                    
                    var bytes = await File.ReadAllBytesAsync(absPath);
                    return Convert.ToBase64String(bytes);
                }
                throw new ArgumentException("Missing 'path'");

            case "fsWrite":
                if (parameters.TryGetValue("path", out var writePathEl) && parameters.TryGetValue("content", out var contentEl))
                {
                    var path = writePathEl.GetString() ?? "";
                    var absPath = ResolvePath(callerId, path);
                    var bytes = Convert.FromBase64String(contentEl.GetString() ?? "");

                    if (!_storage.ValidateAccess(callerId, absPath, true))
                        throw new UnauthorizedAccessException("Write access denied");

                    var dir = Path.GetDirectoryName(absPath);
                    if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    await File.WriteAllBytesAsync(absPath, bytes);
                    return true;
                }
                throw new ArgumentException("Missing 'path' or 'content'");

            case "fsDelete":
                if (parameters.TryGetValue("path", out var delPathEl))
                {
                    var path = delPathEl.GetString() ?? "";
                    var absPath = ResolvePath(callerId, path);
                    _storage.DeleteFile(callerId, absPath);
                    return true;
                }
                throw new ArgumentException("Missing 'path'");

            case "fsExists":
                if (parameters.TryGetValue("path", out var existsPathEl))
                {
                    var path = existsPathEl.GetString() ?? "";
                    var absPath = ResolvePath(callerId, path);
                    return _storage.FileExists(callerId, absPath);
                }
                throw new ArgumentException("Missing 'path'");

            case "fsList":
                if (parameters.TryGetValue("path", out var listPathEl))
                {
                    var path = listPathEl.GetString() ?? "";
                    var absPath = ResolvePath(callerId, path);
                    return _storage.ListFiles(callerId, absPath);
                }
                throw new ArgumentException("Missing 'path'");

            case "storage:vfs:resolve":
            case "fs:vfs:resolve":
                if (parameters.TryGetValue("path", out var vfsPathEl))
                {
                    return _vfs.ResolvePath(callerId, vfsPathEl.GetString() ?? "");
                }
                throw new ArgumentException("Missing 'path'");

            case "storage:blob:get":
            case "fs:blob:get":
                if (parameters.TryGetValue("hash", out var blobHashEl))
                {
                    var blob = await _storage.GetBlobAsync(blobHashEl.GetString() ?? "");
                    return blob != null ? Convert.ToBase64String(blob) : null;
                }
                throw new ArgumentException("Missing 'hash'");

            default:
                throw new NotSupportedException($"Action {action} not supported");
        }
    }

    private string ResolvePath(string callerId, string path)
    {
        // 1. Check VFS protocols
        if (path.StartsWith("game://") || path.StartsWith("paws://"))
            return _vfs.ResolvePath(callerId, path);

        // 2. If it's a relative path, assume plugin sandbox
        if (!Path.IsPathRooted(path))
            return Path.Combine(_storage.GetPluginDataDirectory(callerId), path);

        // 3. Absolute path
        return path;
    }

    public string? GetRequiredScope(string action) => action switch
    {
        "fsRead" or "fsExists" or "fsList" => "sys:storage:read",
        "fsWrite" or "fsDelete" => "sys:storage:write",
        _ => null
    };

    public bool IsHostOnly(string action) => false;
}
