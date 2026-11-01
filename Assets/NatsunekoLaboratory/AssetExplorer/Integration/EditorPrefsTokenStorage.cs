// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using NatsunekoLaboratory.AssetExplorer.Auth;

using UnityEditor;

namespace NatsunekoLaboratory.AssetExplorer.Integration
{
    /// <summary>
    ///     Keeps the login in EditorPrefs: shared by every project on this machine, and separate per API base URL
    ///     so a staging login never replaces the production one.
    /// </summary>
    internal sealed class EditorPrefsTokenStorage : ITokenStorage
    {
        private static string Key => PetrichorSettings.KeyPrefix + "Token:" + PetrichorSettings.ApiBaseUrl;

        public StoredToken Load()
        {
            return StoredToken.FromJson(EditorPrefs.GetString(Key, ""));
        }

        public void Save(StoredToken token)
        {
            EditorPrefs.SetString(Key, token.ToJson());
        }

        public void Clear()
        {
            EditorPrefs.DeleteKey(Key);
        }
    }
}
