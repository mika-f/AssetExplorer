// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Security.Cryptography;
using System.Text;

namespace NatsunekoLaboratory.AssetExplorer.Auth
{
    /// <summary>
    ///     A PKCE (RFC 7636) verifier / challenge pair using the <c>S256</c> method.
    ///     Ported from the teyvat C# SDK (CatalystSharp.Auth.Pkce) to C# 9 / .NET Standard 2.1.
    /// </summary>
    internal sealed class Pkce
    {
        private Pkce(string verifier, string challenge)
        {
            Verifier = verifier;
            Challenge = challenge;
        }

        public string Verifier { get; }

        public string Challenge { get; }

        public string Method => "S256";

        /// <summary>Creates a pair from 32 random bytes.</summary>
        public static Pkce Create()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);

            return FromVerifier(Base64UrlEncode(bytes));
        }

        /// <summary>Builds a pair from a known verifier (e.g. for tests against RFC 7636 vectors).</summary>
        public static Pkce FromVerifier(string verifier)
        {
            if (verifier == null)
                throw new ArgumentNullException(nameof(verifier));

            using (var sha256 = SHA256.Create())
                return new Pkce(verifier, Base64UrlEncode(sha256.ComputeHash(Encoding.UTF8.GetBytes(verifier))));
        }

        internal static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }
    }
}
