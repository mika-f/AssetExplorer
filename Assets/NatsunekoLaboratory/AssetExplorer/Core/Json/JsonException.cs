// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;

namespace NatsunekoLaboratory.AssetExplorer.Json
{
    internal sealed class JsonException : Exception
    {
        public JsonException(string message, int position) : base($"{message} (at {position})")
        {
            Position = position;
        }

        public int Position { get; }
    }
}
