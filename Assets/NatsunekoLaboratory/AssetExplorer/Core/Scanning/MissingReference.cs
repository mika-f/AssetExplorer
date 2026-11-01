// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace NatsunekoLaboratory.AssetExplorer.Scanning
{
    /// <summary>A GUID that is referenced but not present in the project, with every place it was referenced from.</summary>
    internal sealed class MissingReference
    {
        public MissingReference(string guid)
        {
            Guid = guid;
        }

        public string Guid { get; }

        public List<string> Locations { get; } = new List<string>();
    }
}
