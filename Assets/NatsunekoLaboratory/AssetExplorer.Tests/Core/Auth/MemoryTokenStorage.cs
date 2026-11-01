// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using NatsunekoLaboratory.AssetExplorer.Auth;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Auth
{
    /// <summary>Keeps the token as JSON in memory, like EditorPrefs would.</summary>
    internal sealed class MemoryTokenStorage : ITokenStorage
    {
        private string _json;

        public StoredToken Load()
        {
            return StoredToken.FromJson(_json);
        }

        public void Save(StoredToken token)
        {
            _json = token.ToJson();
        }

        public void Clear()
        {
            _json = null;
        }
    }
}
