// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;

namespace NatsunekoLaboratory.AssetExplorer.Auth
{
    internal enum OAuthErrorKind
    {
        /// <summary>The authorization server answered the callback with an <c>error</c> parameter.</summary>
        AuthorizationRequestFailed,

        /// <summary>The callback does not match the expected redirect URI / state / issuer, carries no <c>code</c>, or never arrived.</summary>
        InvalidAuthorizationResponse,

        /// <summary>The token endpoint answered with a non-2xx status or a body that is not a token response.</summary>
        InvalidTokenResponse
    }

    internal sealed class OAuthException : Exception
    {
        public OAuthException(string message, OAuthErrorKind kind, string error = null, string errorDescription = null, int? statusCode = null) : base(message)
        {
            Kind = kind;
            Error = error;
            ErrorDescription = errorDescription;
            StatusCode = statusCode;
        }

        public OAuthErrorKind Kind { get; }

        /// <summary>The OAuth <c>error</c> code (e.g. <c>invalid_grant</c>), when the server sent one.</summary>
        public string Error { get; }

        public string ErrorDescription { get; }

        /// <summary>The HTTP status of a failed token request.</summary>
        public int? StatusCode { get; }
    }
}
