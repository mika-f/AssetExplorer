// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Net;
using System.Net.Http;
using System.Threading;

using NatsunekoLaboratory.AssetExplorer.Auth;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Auth
{
    public class OAuthClientTests
    {
        private const string Redirect = "http://127.0.0.1:57193/callback";
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);

        private static OAuthClient Client(MockHandler handler = null)
        {
            handler = handler ?? new MockHandler(_ => MockHandler.Json(HttpStatusCode.OK, "{}"));
            return new OAuthClient("client-1", "https://accounts.test/authorize", "https://api.test/token", new HttpClient(handler), () => Now);
        }

        private static string TokenJson(string extra = "")
        {
            return "{\"access_token\":\"at\",\"refresh_token\":\"rt\",\"token_type\":\"Bearer\",\"scope\":\"read write\"" + extra + "}";
        }

        [Test]
        public void Authorize_url_has_all_params()
        {
            var pkce = Pkce.FromVerifier("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk");
            var url = Client().GetAuthorizeUrl(Redirect, pkce, "s t").ToString();

            Assert.That(url, Does.StartWith("https://accounts.test/authorize?"));
            Assert.That(url, Does.Contain("response_type=code"));
            Assert.That(url, Does.Contain("client_id=client-1"));
            Assert.That(url, Does.Contain("redirect_uri=http%3A%2F%2F127.0.0.1%3A57193%2Fcallback"));
            Assert.That(url, Does.Contain("state=s+t"));
            Assert.That(url, Does.Contain("code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"));
            Assert.That(url, Does.Contain("code_challenge_method=S256"));
        }

        [Test]
        public void Issuer_is_the_authorize_origin()
        {
            Assert.That(Client().Issuer, Is.EqualTo("https://accounts.test"));
        }

        [Test]
        public void Callback_returns_code()
        {
            var code = Client().GetAuthorizationCode(new Uri(Redirect + "?code=abc&state=s&iss=https%3A%2F%2Faccounts.test"), "s", Redirect, "https://accounts.test");

            Assert.That(code, Is.EqualTo("abc"));
        }

        [Test]
        public void Callback_without_iss_is_accepted()
        {
            Assert.That(Client().GetAuthorizationCode(new Uri(Redirect + "?code=abc&state=s"), "s", Redirect, "https://accounts.test"), Is.EqualTo("abc"));
        }

        [Test]
        public void Callback_state_mismatch_throws_InvalidAuthorizationResponse()
        {
            var e = Assert.Throws<OAuthException>(() => Client().GetAuthorizationCode(new Uri(Redirect + "?code=abc&state=other"), "s", Redirect, null));

            Assert.That(e.Kind, Is.EqualTo(OAuthErrorKind.InvalidAuthorizationResponse));
        }

        [Test]
        public void Callback_for_another_redirect_throws_InvalidAuthorizationResponse()
        {
            var e = Assert.Throws<OAuthException>(() => Client().GetAuthorizationCode(new Uri("http://127.0.0.1:1/other?code=abc&state=s"), "s", Redirect, null));

            Assert.That(e.Kind, Is.EqualTo(OAuthErrorKind.InvalidAuthorizationResponse));
        }

        [Test]
        public void Callback_error_throws_AuthorizationRequestFailed()
        {
            var e = Assert.Throws<OAuthException>(() => Client().GetAuthorizationCode(new Uri(Redirect + "?error=access_denied&error_description=no&state=s"), "s", Redirect, null));

            Assert.That(e.Kind, Is.EqualTo(OAuthErrorKind.AuthorizationRequestFailed));
            Assert.That(e.Error, Is.EqualTo("access_denied"));
            Assert.That(e.ErrorDescription, Is.EqualTo("no"));
        }

        [Test]
        public void Callback_iss_mismatch_throws()
        {
            var e = Assert.Throws<OAuthException>(() => Client().GetAuthorizationCode(new Uri(Redirect + "?code=abc&state=s&iss=https%3A%2F%2Fevil.test"), "s", Redirect, "https://accounts.test"));

            Assert.That(e.Kind, Is.EqualTo(OAuthErrorKind.InvalidAuthorizationResponse));
        }

        [Test]
        public void Code_exchange_posts_form_without_secret()
        {
            var handler = new MockHandler(_ => MockHandler.Json(HttpStatusCode.OK, TokenJson()));
            var token = AsyncHelper.Run(() => Client(handler).GetAccessTokenByCodeAsync("the code", Redirect, Pkce.FromVerifier("verifier"), CancellationToken.None));

            var body = handler.Requests[0].Body;
            Assert.That(handler.Requests[0].Uri.ToString(), Is.EqualTo("https://api.test/token"));
            Assert.That(body, Does.Contain("grant_type=authorization_code"));
            Assert.That(body, Does.Contain("code=the+code"));
            Assert.That(body, Does.Contain("code_verifier=verifier"));
            Assert.That(body, Does.Contain("client_id=client-1"));
            Assert.That(body, Does.Contain("redirect_uri=http%3A%2F%2F127.0.0.1%3A57193%2Fcallback"));
            Assert.That(body, Does.Not.Contain("client_secret"));
            Assert.That(token.AccessToken, Is.EqualTo("at"));
            Assert.That(token.RefreshToken, Is.EqualTo("rt"));
        }

        [Test]
        public void Refresh_posts_refresh_grant()
        {
            var handler = new MockHandler(_ => MockHandler.Json(HttpStatusCode.OK, TokenJson()));
            AsyncHelper.Run(() => Client(handler).GetAccessTokenByRefreshTokenAsync("old", CancellationToken.None));

            var body = handler.Requests[0].Body;
            Assert.That(body, Does.Contain("grant_type=refresh_token"));
            Assert.That(body, Does.Contain("refresh_token=old"));
            Assert.That(body, Does.Contain("client_id=client-1"));
            Assert.That(body, Does.Not.Contain("client_secret"));
        }

        [Test]
        public void Token_expiry_from_expires_in()
        {
            var handler = new MockHandler(_ => MockHandler.Json(HttpStatusCode.OK, TokenJson(",\"expires_in\":604800")));
            var token = AsyncHelper.Run(() => Client(handler).GetAccessTokenByRefreshTokenAsync("old", CancellationToken.None));

            Assert.That(token.ExpiresAt, Is.EqualTo(Now.AddDays(7)));
        }

        [Test]
        public void Token_expiry_prefers_expires_at()
        {
            var handler = new MockHandler(_ => MockHandler.Json(HttpStatusCode.OK, TokenJson(",\"expires_at\":\"2026-11-09T00:00:00.000Z\",\"expires_in\":1")));
            var token = AsyncHelper.Run(() => Client(handler).GetAccessTokenByRefreshTokenAsync("old", CancellationToken.None));

            Assert.That(token.ExpiresAt, Is.EqualTo(new DateTimeOffset(2026, 11, 9, 0, 0, 0, TimeSpan.Zero)));
        }

        [Test]
        public void Non_2xx_token_response_throws_InvalidTokenResponse_with_status()
        {
            var handler = new MockHandler(_ => MockHandler.Json(HttpStatusCode.BadRequest, "{\"error\":\"invalid_grant\",\"error_description\":\"bad\",\"message\":\"bad\",\"code\":400}"));

            var e = Assert.Throws<OAuthException>(() => AsyncHelper.Run(() => Client(handler).GetAccessTokenByRefreshTokenAsync("old", CancellationToken.None)));

            Assert.That(e, Is.Not.Null);
            Assert.That(e.Kind, Is.EqualTo(OAuthErrorKind.InvalidTokenResponse));
            Assert.That(e.Error, Is.EqualTo("invalid_grant"));
            Assert.That(e.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public void Token_response_without_tokens_throws()
        {
            var handler = new MockHandler(_ => MockHandler.Json(HttpStatusCode.OK, "{\"access_token\":\"at\"}"));

            var e = Assert.Throws<OAuthException>(() => AsyncHelper.Run(() => Client(handler).GetAccessTokenByRefreshTokenAsync("old", CancellationToken.None)));

            Assert.That(e?.Kind, Is.EqualTo(OAuthErrorKind.InvalidTokenResponse));
        }
    }
}
