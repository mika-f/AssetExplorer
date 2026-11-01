// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

namespace NatsunekoLaboratory.AssetExplorer.Contribute
{
    internal sealed class ManifestAsset
    {
        public ManifestAsset(string guid, string path, string type)
        {
            Guid = guid;
            Path = path;
            Type = type;
        }

        public string Guid { get; }

        /// <summary>Project-relative path with <c>/</c> separators, e.g. <c>Assets/Foo/Bar.mat</c>.</summary>
        public string Path { get; }

        /// <summary>The importer name from the <c>.meta</c> file (e.g. <c>TextureImporter</c>), or null.</summary>
        public string Type { get; }
    }
}
