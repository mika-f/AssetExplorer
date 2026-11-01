// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using NatsunekoLaboratory.AssetExplorer.Json;

namespace NatsunekoLaboratory.AssetExplorer.Contribute
{
    internal sealed class DetectedPackageInfo
    {
        /// <summary>The UPM/VPM package name (<c>com.example.foo</c>), used as a <c>package</c> identifier.</summary>
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string Version { get; set; }

        public string Description { get; set; }
    }

    /// <summary>
    ///     Prefills the contribution form from a <c>package.json</c>, or guesses name and version from a .unitypackage file name.
    /// </summary>
    internal static class PackageInfoDetector
    {
        private static readonly Regex VersionPattern = new Regex(@"v?(\d+(?:\.\d+)+(?:[-+][0-9A-Za-z.-]+)?)", RegexOptions.Compiled);

        public static DetectedPackageInfo FromPackageJson(string json)
        {
            object root;
            try
            {
                root = JsonReader.Parse(json);
            }
            catch (JsonException)
            {
                return null;
            }

            if (!(root is Dictionary<string, object>))
                return null;

            return new DetectedPackageInfo
            {
                Name = root.Str("name"),
                DisplayName = root.Str("displayName"),
                Version = root.Str("version"),
                Description = root.Str("description")
            };
        }

        public static DetectedPackageInfo FromFileName(string fileName)
        {
            var stem = Path.GetFileNameWithoutExtension(fileName ?? "");
            var match = VersionPattern.Match(stem);
            if (!match.Success)
                return new DetectedPackageInfo { DisplayName = stem.Trim() };

            var name = stem.Substring(0, match.Index).Trim('_', '-', ' ', '.');
            return new DetectedPackageInfo { DisplayName = name.Length > 0 ? name : stem.Trim(), Version = match.Groups[1].Value };
        }
    }
}
