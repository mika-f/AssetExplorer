// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using NatsunekoLaboratory.AssetExplorer.Auth;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Auth
{
    public class AuthSessionTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);

        private MemoryStorage _storage;
        private MockHandler _handler;
        private Func<RecordedRequest, HttpResponseMessage> _api;
        private Func<RecordedRequest, HttpResponseMessage> _token;
        private HttpClient _http;

        [SetUp]
        public void SetUp()
        {
            _storage = new MemoryStorage();
            _api = _ => MockHandler.Json(HttpStatusCode.OK, "{}");
            _token = _ => MockHandler.Json(HttpStatusCode.OK, "{\"access_token\":\"new-at\",\"refresh_token\":\"new-rt\",\"token_type\":\"Bearer\",\"expires_in\":604800}");
            _handler = new MockHandler(r => r.Uri.AbsolutePath == "/token" ? _token(r) : _api(r));
            _http = new HttpClient(_handler);
        }

        private AuthSession Session(int port = LoopbackReceiver.DefaultPort)
        {
            return new AuthSession(_storage, () => new OAuthClient("client-1", "https://accounts.test/authorize", "https://api.test/token", _http, () => Now), () => Now, () => new LoopbackReceiver(port));
        }

        private void Store(TimeSpan expiresIn)
        {
            _storage.Save(new StoredToken { AccessToken = "at", RefreshToken = "rt", ExpiresAt = Now + expiresIn });
        }

        private int TokenCalls => _handler.Requests.Count(w => w.Uri.AbsolutePath == "/token");

        [Test]
        public void Not_logged_in_throws_AuthRequired()
        {
            var session = Session();

            Assert.That(session.IsLoggedIn, Is.False);
            Assert.Throws<AuthRequiredException>(() => AsyncHelper.Run(() => session.GetAccessTokenAsync(CancellationToken.None)));
        }

        [Test]
        public void Fresh_token_is_returned_without_refresh()
        {
            Store(TimeSpan.FromDays(3));

            Assert.That(AsyncHelper.Run(() => Session().GetAccessTokenAsync(CancellationToken.None)), Is.EqualTo("at"));
            Assert.That(_handler.Requests, Is.Empty);
        }

        [Test]
        public void Token_near_expiry_is_refreshed_and_saved()
        {
            Store(TimeSpan.FromHours(2));
            var session = Session();
            var changed = 0;
            session.Changed += () => changed++;

            Assert.That(AsyncHelper.Run(() => session.GetAccessTokenAsync(CancellationToken.None)), Is.EqualTo("new-at"));
            Assert.That(TokenCalls, Is.EqualTo(1));
            Assert.That(_handler.Requests[0].Body, Does.Contain("refresh_token=rt"));
            Assert.That(_storage.Load().AccessToken, Is.EqualTo("new-at"));
            Assert.That(_storage.Load().RefreshToken, Is.EqualTo("new-rt"));
            Assert.That(_storage.Load().ExpiresAt, Is.EqualTo(Now.AddDays(7)));
            Assert.That(changed, Is.EqualTo(1));
        }

        [Test]
        public void Unknown_expiry_is_refreshed()
        {
            _storage.Save(new StoredToken { AccessToken = "at", RefreshToken = "rt" });

            Assert.That(AsyncHelper.Run(() => Session().GetAccessTokenAsync(CancellationToken.None)), Is.EqualTo("new-at"));
        }

        [Test]
        public void Concurrent_callers_share_one_refresh()
        {
            Store(TimeSpan.FromHours(2));
            _handler.Delay = TimeSpan.FromMilliseconds(200);
            var session = Session();

            var tokens = AsyncHelper.Run(() => Task.WhenAll(session.GetAccessTokenAsync(CancellationToken.None), session.GetAccessTokenAsync(CancellationToken.None)));

            Assert.That(tokens, Is.EqualTo(new[] { "new-at", "new-at" }));
            Assert.That(TokenCalls, Is.EqualTo(1));
        }

        [Test]
        public void Send_retries_once_after_401()
        {
            Store(TimeSpan.FromDays(3));
            var apiCalls = 0;
            _api = r => ++apiCalls == 1 ? MockHandler.Json(HttpStatusCode.Unauthorized, "{\"message\":\"unauthorized\"}") : MockHandler.Json(HttpStatusCode.OK, "{}");

            var response = AsyncHelper.Run(() => Session().SendAsync(_http, token => new HttpRequestMessage(HttpMethod.Get, "https://api.test/me") { Headers = { { "Authorization", "Bearer " + token } } }, CancellationToken.None));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(TokenCalls, Is.EqualTo(1));
            Assert.That(apiCalls, Is.EqualTo(2));
            Assert.That(_handler.Requests.Where(w => w.Uri.AbsolutePath == "/me").Select(w => w.Authorization), Is.EqualTo(new[] { "Bearer at", "Bearer new-at" }));
        }

        [Test]
        public void Refresh_rejected_clears_tokens_and_throws_AuthRequired()
        {
            Store(TimeSpan.FromHours(2));
            _token = _ => MockHandler.Json(HttpStatusCode.BadRequest, "{\"error\":\"invalid_grant\",\"message\":\"不正なリフレッシュトークンです\"}");
            var session = Session();
            var changed = 0;
            session.Changed += () => changed++;

            var e = Assert.Throws<AuthRequiredException>(() => AsyncHelper.Run(() => session.GetAccessTokenAsync(CancellationToken.None)));

            Assert.That(e.Message, Is.EqualTo("ログインし直してください"));
            Assert.That(_storage.Load(), Is.Null);
            Assert.That(session.IsLoggedIn, Is.False);
            Assert.That(changed, Is.EqualTo(1));
        }

        [Test]
        public void Network_error_on_refresh_keeps_tokens()
        {
            Store(TimeSpan.FromHours(2));
            _token = _ => throw new HttpRequestException("offline");

            Assert.Throws<HttpRequestException>(() => AsyncHelper.Run(() => Session().GetAccessTokenAsync(CancellationToken.None)));
            Assert.That(_storage.Load().AccessToken, Is.EqualTo("at"));
        }

        [Test]
        public void Login_exchanges_code_and_saves_tokens()
        {
            var session = Session(57296);
            var changed = 0;
            session.Changed += () => changed++;

            using (var browser = new HttpClient())
            {
                AsyncHelper.Run(() => session.LoginAsync(url =>
                {
                    var state = url.Query.Split('&').First(w => w.StartsWith("state=")).Substring("state=".Length);
                    Task.Run(() => browser.GetAsync($"http://127.0.0.1:57296/callback?code=the-code&state={state}&iss=https%3A%2F%2Faccounts.test"));
                }, CancellationToken.None));
            }

            var exchange = _handler.Requests.Single(w => w.Uri.AbsolutePath == "/token");
            Assert.That(exchange.Body, Does.Contain("code=the-code"));
            Assert.That(exchange.Body, Does.Contain("redirect_uri=http%3A%2F%2F127.0.0.1%3A57296%2Fcallback"));
            Assert.That(session.IsLoggedIn, Is.True);
            Assert.That(_storage.Load().AccessToken, Is.EqualTo("new-at"));
            Assert.That(changed, Is.EqualTo(1));
        }

        [Test]
        public void Profile_and_logout_update_storage()
        {
            Store(TimeSpan.FromDays(3));
            var session = Session();

            session.SetProfile(new NatsunekoLaboratory.AssetExplorer.Api.User { ScreenName = "natsuneko", DisplayName = "なつねこ" });
            Assert.That(_storage.Load().ScreenName, Is.EqualTo("natsuneko"));

            session.Logout();
            Assert.That(_storage.Load(), Is.Null);
            Assert.That(session.IsLoggedIn, Is.False);
        }

        [Test]
        public void Reload_reads_storage_again()
        {
            var session = Session();
            Store(TimeSpan.FromDays(3));

            Assert.That(session.IsLoggedIn, Is.False);
            session.Reload();
            Assert.That(session.IsLoggedIn, Is.True);
        }

        [Test]
        public void StoredToken_round_trips_through_json()
        {
            var token = new StoredToken { AccessToken = "a", RefreshToken = "r", ExpiresAt = Now, ScreenName = "s", DisplayName = "d" };
            var copy = StoredToken.FromJson(token.ToJson());

            Assert.That(copy.AccessToken, Is.EqualTo("a"));
            Assert.That(copy.RefreshToken, Is.EqualTo("r"));
            Assert.That(copy.ExpiresAt, Is.EqualTo(Now));
            Assert.That(copy.ScreenName, Is.EqualTo("s"));
            Assert.That(copy.DisplayName, Is.EqualTo("d"));
            Assert.That(StoredToken.FromJson("not json"), Is.Null);
            Assert.That(StoredToken.FromJson(null), Is.Null);
        }

        private sealed class MemoryStorage : ITokenStorage
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
}
