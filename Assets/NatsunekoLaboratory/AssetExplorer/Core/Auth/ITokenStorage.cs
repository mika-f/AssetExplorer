// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;

namespace NatsunekoLaboratory.AssetExplorer.Auth
{
    internal interface ITokenStorage
    {
        /// <summary>Returns the stored login, or null.</summary>
        StoredToken Load();

        void Save(StoredToken token);

        void Clear();
    }

    /// <summary>Thrown when an operation needs a login and there is none (or it can no longer be refreshed).</summary>
    internal sealed class AuthRequiredException : Exception
    {
        public AuthRequiredException() : base("ログインし直してください") { }
    }
}
