// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.IO;

using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NatsunekoLaboratory.AssetExplorer.Integration
{
    internal static class ProjectPaths
    {
        /// <summary>
        ///     Maps a project path to a file on disk. <c>Packages/…</c> paths of registry or git packages live in the package cache,
        ///     not under the project folder.
        /// </summary>
        public static string ToPhysical(string assetPath)
        {
            if (assetPath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var package = PackageInfo.FindForAssetPath(assetPath);
                if (package != null && !string.IsNullOrEmpty(package.resolvedPath))
                {
                    var relative = assetPath.Substring(package.assetPath.Length).TrimStart('/');
                    return relative.Length == 0 ? package.resolvedPath : Path.Combine(package.resolvedPath, relative);
                }
            }

            return Path.GetFullPath(assetPath);
        }

        public static bool Exists(string assetPath)
        {
            var physical = ToPhysical(assetPath);
            return File.Exists(physical) || Directory.Exists(physical);
        }
    }
}
