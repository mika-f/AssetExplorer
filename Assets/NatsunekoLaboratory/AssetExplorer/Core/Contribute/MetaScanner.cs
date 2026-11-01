// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NatsunekoLaboratory.AssetExplorer.Contribute
{
    /// <summary>
    ///     Builds manifest entries from the <c>.meta</c> files under a folder on disk. Folders are never entries.
    /// </summary>
    internal static class MetaScanner
    {
        /// <param name="physicalRoot">The folder on disk.</param>
        /// <param name="projectPrefix">The project-relative path of that folder, e.g. <c>Assets/Foo</c> or <c>Packages/com.example.foo</c>.</param>
        public static List<ManifestAsset> Scan(string physicalRoot, string projectPrefix)
        {
            var root = Path.GetFullPath(physicalRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var prefix = projectPrefix.Replace('\\', '/').TrimEnd('/');
            var assets = new List<ManifestAsset>();

            foreach (var metaPath in Directory.EnumerateFiles(root, "*.meta", SearchOption.AllDirectories))
            {
                var assetPath = metaPath.Substring(0, metaPath.Length - ".meta".Length);
                var relative = assetPath.Substring(root.Length + 1).Replace('\\', '/');

                // Unity ignores names starting with "." and folders ending with "~"
                if (relative.Split('/').Any(w => w.StartsWith(".", StringComparison.Ordinal) || w.EndsWith("~", StringComparison.Ordinal)))
                    continue;

                if (!File.Exists(assetPath))
                    continue;

                var meta = MetaFile.Parse(File.ReadAllText(metaPath));
                if (meta == null || meta.IsFolder)
                    continue;

                assets.Add(new ManifestAsset(meta.Guid, $"{prefix}/{relative}", meta.ImporterType));
            }

            assets.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
            return assets;
        }
    }
}
