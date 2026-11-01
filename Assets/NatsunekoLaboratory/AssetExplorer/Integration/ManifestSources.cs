// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using NatsunekoLaboratory.AssetExplorer.Contribute;

using UnityEditor;

using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NatsunekoLaboratory.AssetExplorer.Integration
{
    /// <summary>Builds manifest entries from project folders, packages and the current selection by reading their .meta files.</summary>
    internal static class ManifestSources
    {
        public static List<ManifestAsset> FromFolder(string assetFolder)
        {
            return Normalize(MetaScanner.Scan(ProjectPaths.ToPhysical(assetFolder), assetFolder));
        }

        public static List<ManifestAsset> FromPackage(PackageInfo package)
        {
            return Normalize(MetaScanner.Scan(package.resolvedPath, package.assetPath));
        }

        public static List<ManifestAsset> FromSelection()
        {
            var assets = new List<ManifestAsset>();

            foreach (var path in Selection.assetGUIDs.Select(AssetDatabase.GUIDToAssetPath).Where(w => !string.IsNullOrEmpty(w)).Distinct())
            {
                if (AssetDatabase.IsValidFolder(path))
                {
                    assets.AddRange(FromFolder(path));
                    continue;
                }

                var metaPath = ProjectPaths.ToPhysical(path) + ".meta";
                if (!File.Exists(metaPath))
                    continue;

                var meta = MetaFile.Parse(File.ReadAllText(metaPath));
                if (meta != null && !meta.IsFolder)
                    assets.Add(new ManifestAsset(meta.Guid, path, meta.ImporterType));
            }

            return Normalize(assets);
        }

        /// <summary>Returns the text of <c>package.json</c> at the root of a folder or package, or null.</summary>
        public static string FindPackageJson(string assetFolderOrPackageRoot)
        {
            var path = Path.Combine(ProjectPaths.ToPhysical(assetFolderOrPackageRoot), "package.json");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        private static List<ManifestAsset> Normalize(IEnumerable<ManifestAsset> assets)
        {
            var normalized = ManifestValidator.Normalize(assets);
            normalized.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            return normalized;
        }
    }
}
