using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Paws.Core.Abstractions.Models;

namespace Paws.Host.Services.Core
{
    public class PluginSecurityAnalysisResult
    {
        public bool IsSafe { get; set; }
        public List<string> Violations { get; set; } = new();
    }

    public static class PluginSecurityScanner
    {
        private static readonly HashSet<string> ForbiddenNamespaces = new()
        {
            "System.IO",
            "System.Reflection",
            "System.Runtime.InteropServices",
            "Microsoft.Win32"
        };

        private static readonly HashSet<string> AllowedTypes = new()
        {
            // System.IO
            "System.IO.Path",
            "System.IO.Stream",
            "System.IO.MemoryStream",
            "System.IO.BinaryReader",
            "System.IO.BinaryWriter",
            "System.IO.SeekOrigin",
            "System.IO.IOException",
            "System.IO.FileMode",
            "System.IO.FileAccess",
            "System.IO.SearchOption",

            // System.Reflection (Metadata only)
            "System.Reflection.AssemblyTitleAttribute",
            "System.Reflection.AssemblyProductAttribute",
            "System.Reflection.AssemblyCopyrightAttribute",
            "System.Reflection.AssemblyTrademarkAttribute",
            "System.Reflection.AssemblyCultureAttribute",
            "System.Reflection.AssemblyVersionAttribute",
            "System.Reflection.AssemblyFileVersionAttribute",
            "System.Reflection.AssemblyConfigurationAttribute",
            "System.Reflection.AssemblyDescriptionAttribute",
            "System.Reflection.AssemblyCompanyAttribute",

            // Realm Support (Injected by Realm weaver)
            "System.Reflection.IReflectableType",
            "System.Reflection.ObfuscationAttribute",
            "System.Reflection.TypeInfo"
        };

        public static PluginSecurityAnalysisResult Analyze(Stream dllStream, PluginManifest manifest)
        {
            var result = new PluginSecurityAnalysisResult { IsSafe = true };

            // If the plugin explicitly requests unsafe access, we skip or note it
            if (manifest.Permissions.Contains("unsafe-access"))
            {
                return result;
            }

            using var peReader = new PEReader(dllStream, PEStreamOptions.LeaveOpen);

            if (!peReader.HasMetadata)
            {
                result.IsSafe = false;
                result.Violations.Add("DLL has no metadata.");
                return result;
            }

            var metadata = peReader.GetMetadataReader();

            // Scan Type References
            foreach (var typeRefHandle in metadata.TypeReferences)
            {
                var typeRef = metadata.GetTypeReference(typeRefHandle);
                var @namespace = metadata.GetString(typeRef.Namespace);
                var name = metadata.GetString(typeRef.Name);
                var fullTypeName = string.IsNullOrEmpty(@namespace) ? name : $"{@namespace}.{name}";

                if (IsForbidden(fullTypeName, @namespace))
                {
                    result.IsSafe = false;
                    result.Violations.Add($"Illegal type reference: {fullTypeName}");
                }
            }

            // Scan Member References (Methods/Fields)
            foreach (var memberRefHandle in metadata.MemberReferences)
            {
                var memberRef = metadata.GetMemberReference(memberRefHandle);
                if (memberRef.Parent.Kind == HandleKind.TypeReference)
                {
                    var typeRef = metadata.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
                    var @namespace = metadata.GetString(typeRef.Namespace);
                    var name = metadata.GetString(typeRef.Name);
                    var fullTypeName = string.IsNullOrEmpty(@namespace) ? name : $"{@namespace}.{name}";

                    if (IsForbidden(fullTypeName, @namespace))
                    {
                        result.IsSafe = false;
                        var memberName = metadata.GetString(memberRef.Name);
                        result.Violations.Add($"Illegal member call: {fullTypeName}.{memberName}");
                    }
                }
            }

            return result;
        }

        private static bool IsForbidden(string fullTypeName, string @namespace)
        {
            if (AllowedTypes.Contains(fullTypeName)) return false;
            return ForbiddenNamespaces.Any(ns => @namespace.StartsWith(ns));
        }
    }
}
