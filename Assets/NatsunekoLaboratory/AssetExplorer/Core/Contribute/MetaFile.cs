// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

namespace NatsunekoLaboratory.AssetExplorer.Contribute
{
    /// <summary>
    ///     The parts of a Unity <c>.meta</c> file a manifest needs. Reading the text directly (instead of asking the AssetDatabase)
    ///     gives the same result for project folders, packages and .unitypackage archives, so their manifests hash alike.
    /// </summary>
    internal sealed class MetaFile
    {
        public string Guid { get; private set; }

        /// <summary>The first top-level key ending in <c>Importer</c>, e.g. <c>TextureImporter</c>.</summary>
        public string ImporterType { get; private set; }

        public bool IsFolder { get; private set; }

        public static MetaFile Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            var meta = new MetaFile();
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == ' ' || line[0] == '\t')
                    continue;

                var colon = line.IndexOf(':');
                if (colon <= 0)
                    continue;

                var key = line.Substring(0, colon);
                var value = line.Substring(colon + 1).Trim();

                if (key == "guid" && meta.Guid == null)
                    meta.Guid = value.ToLowerInvariant();
                else if (key == "folderAsset" && value == "yes")
                    meta.IsFolder = true;
                else if (meta.ImporterType == null && key.EndsWith("Importer", System.StringComparison.Ordinal))
                    meta.ImporterType = key;
            }

            return string.IsNullOrEmpty(meta.Guid) ? null : meta;
        }
    }
}
