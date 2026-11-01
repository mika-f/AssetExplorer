// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;

namespace NatsunekoLaboratory.AssetExplorer.Auth
{
    /// <summary>The result of a token exchange or refresh.</summary>
    internal sealed class Token
    {
        public string AccessToken { get; set; }

        public string RefreshToken { get; set; }

        public string TokenType { get; set; }

        public string Scope { get; set; }

        /// <summary>From <c>expires_at</c>, or now + <c>expires_in</c>; null when the server sent neither.</summary>
        public DateTimeOffset? ExpiresAt { get; set; }
    }
}
