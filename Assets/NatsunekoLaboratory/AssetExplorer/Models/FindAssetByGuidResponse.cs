// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace NatsunekoLaboratory.AssetExplorer.Models
{
    internal class FindAssetByGuidResponse
    {
        public string Id { get; set; }

        public string WellKnownName { get; set; }

        public string WellKnownType { get; set; }

        public PackageResponse Package { get; set; }

        public List<string> Versions { get; set; }

        internal class PackageResponse
        {
            public string Id { get; set; }

            public string Name { get; set; }

            public string Url { get; set; }
        }
    }
}