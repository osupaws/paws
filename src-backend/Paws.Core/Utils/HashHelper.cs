using System;
using System.Security.Cryptography;
using System.Text;

namespace Paws.Core.Utils;

/// <summary>
/// Utility for computing cryptographic hashes.
/// </summary>
public static class HashHelper
{
    public static string ComputeSha256(string content)
    {
        var inputBytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
