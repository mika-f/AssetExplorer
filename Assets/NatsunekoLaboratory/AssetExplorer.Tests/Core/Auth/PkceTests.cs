// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using NatsunekoLaboratory.AssetExplorer.Auth;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Auth
{
    public class PkceTests
    {
        [Test]
        public void Rfc7636_vector()
        {
            var pkce = Pkce.FromVerifier("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk");

            Assert.That(pkce.Challenge, Is.EqualTo("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"));
            Assert.That(pkce.Method, Is.EqualTo("S256"));
        }

        [Test]
        public void Create_produces_43_char_url_safe_verifier()
        {
            var a = Pkce.Create();
            var b = Pkce.Create();

            Assert.That(a.Verifier.Length, Is.EqualTo(43));
            Assert.That(a.Verifier, Does.Match("^[A-Za-z0-9_-]+$"));
            Assert.That(a.Verifier, Is.Not.EqualTo(b.Verifier));
        }
    }
}
