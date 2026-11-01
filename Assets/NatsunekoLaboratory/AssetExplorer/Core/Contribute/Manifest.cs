// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

using NatsunekoLaboratory.AssetExplorer.Json;

namespace NatsunekoLaboratory.AssetExplorer.Contribute
{
    /// <summary>
    ///     A submission body (<c>petrichor/v1</c>). The same JSON is sent to the API and saved for the website.
    /// </summary>
    internal sealed class Manifest
    {
        public const string Format = "petrichor/v1";

        public PackageSpec Package { get; set; }

        public string Version { get; set; }

        public List<ManifestAsset> Assets { get; set; } = new List<ManifestAsset>();

        public Dictionary<string, object> ToJsonObject()
        {
            return new Dictionary<string, object>
            {
                { "format", Format },
                { "package", Package?.ToJsonObject() },
                { "version", Version?.Trim() },
                { "assets", Assets.Select(ToJsonObject).ToList() }
            };
        }

        public string ToJson()
        {
            return JsonWriter.Write(ToJsonObject());
        }

        private static object ToJsonObject(ManifestAsset asset)
        {
            var json = new Dictionary<string, object> { { "guid", asset.Guid }, { "path", asset.Path } };
            if (!string.IsNullOrEmpty(asset.Type))
                json["type"] = asset.Type;

            return json;
        }
    }
}
