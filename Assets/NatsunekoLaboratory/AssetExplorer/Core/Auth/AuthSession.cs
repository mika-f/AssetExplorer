// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using NatsunekoLaboratory.AssetExplorer.Api;

namespace NatsunekoLaboratory.AssetExplorer.Auth
{
    /// <summary>
    ///     Owns the login: browser sign-in, token persistence, refresh before expiry, and one retry after a 401.
    /// </summary>
    internal sealed class AuthSession
    {
        private static readonly TimeSpan LoginTimeout = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan RefreshMargin = TimeSpan.FromDays(1);

        private readonly Func<DateTimeOffset> _clock;
        private readonly object _gate = new object();
        private readonly Func<OAuthClient> _oauth;
        private readonly Func<LoopbackReceiver> _receiver;
        private readonly ITokenStorage _storage;
        private Task<string> _refreshing;

        public AuthSession(ITokenStorage storage, Func<OAuthClient> oauth, Func<DateTimeOffset> clock, Func<LoopbackReceiver> receiver = null)
        {
            _storage = storage;
            _oauth = oauth;
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
            _receiver = receiver ?? (() => new LoopbackReceiver());

            Current = _storage.Load();
        }

        public StoredToken Current { get; private set; }

        public bool IsLoggedIn => !string.IsNullOrEmpty(Current?.AccessToken);

        /// <summary>Raised after login, logout, refresh, a profile update, or a reload. May be raised off the main thread.</summary>
        public event Action Changed;

        public void Reload()
        {
            Current = _storage.Load();
            Changed?.Invoke();
        }

        public async Task<string> GetAccessTokenAsync(CancellationToken ct)
        {
            var current = Current;
            if (string.IsNullOrEmpty(current?.AccessToken))
                throw new AuthRequiredException();

            if (current.ExpiresAt.HasValue && current.ExpiresAt.Value > _clock() + RefreshMargin)
                return current.AccessToken;

            return await RefreshSharedAsync(current.AccessToken).WithCancellation(ct);
        }

        /// <summary>
        ///     Sends an authenticated request. <paramref name="createRequest" /> receives the access token and builds a fresh request;
        ///     after a 401 the token is refreshed once and the request is sent again.
        /// </summary>
        public async Task<HttpResponseMessage> SendAsync(HttpClient http, Func<string, HttpRequestMessage> createRequest, CancellationToken ct)
        {
            var token = await GetAccessTokenAsync(ct);
            var response = await SendOnceAsync(http, createRequest, token, ct);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return response;

            response.Dispose();
            token = await RefreshSharedAsync(token).WithCancellation(ct);
            return await SendOnceAsync(http, createRequest, token, ct);
        }

        public async Task LoginAsync(Action<Uri> openBrowser, CancellationToken ct)
        {
            var oauth = _oauth();
            var pkce = Pkce.Create();
            var state = Guid.NewGuid().ToString("N");

            using (var receiver = _receiver())
            {
                receiver.Start();
                openBrowser(oauth.GetAuthorizeUrl(receiver.CallbackUri, pkce, state));

                var callback = await receiver.WaitForCallbackAsync(LoginTimeout, ct);
                var code = oauth.GetAuthorizationCode(callback, state, receiver.CallbackUri, oauth.Issuer);
                var token = await oauth.GetAccessTokenByCodeAsync(code, receiver.CallbackUri, pkce, ct);

                Save(new StoredToken { AccessToken = token.AccessToken, RefreshToken = token.RefreshToken, ExpiresAt = token.ExpiresAt });
            }
        }

        public void SetProfile(User user)
        {
            var current = Current;
            if (current == null)
                return;

            current.ScreenName = user?.ScreenName;
            current.DisplayName = user?.DisplayName;
            Save(current);
        }

        public void Logout()
        {
            _storage.Clear();
            Current = null;
            Changed?.Invoke();
        }

        private static async Task<HttpResponseMessage> SendOnceAsync(HttpClient http, Func<string, HttpRequestMessage> createRequest, string token, CancellationToken ct)
        {
            using (var request = createRequest(token))
                return await http.SendAsync(request, ct);
        }

        private void Save(StoredToken token)
        {
            _storage.Save(token);
            Current = token;
            Changed?.Invoke();
        }

        /// <summary>
        ///     Refreshes once for every caller that saw <paramref name="staleToken" />. A caller whose token was already replaced
        ///     gets the new one without another refresh.
        /// </summary>
        private Task<string> RefreshSharedAsync(string staleToken)
        {
            lock (_gate)
            {
                if (_refreshing != null)
                    return _refreshing;

                var current = Current;
                if (current != null && current.AccessToken != staleToken && !string.IsNullOrEmpty(current.AccessToken))
                    return Task.FromResult(current.AccessToken);

                var task = RefreshCoreAsync(current);
                _refreshing = task;
                task.ContinueWith(_ =>
                {
                    lock (_gate)
                        if (_refreshing == task)
                            _refreshing = null;
                }, TaskScheduler.Default);

                return task;
            }
        }

        private async Task<string> RefreshCoreAsync(StoredToken current)
        {
            if (string.IsNullOrEmpty(current?.RefreshToken))
            {
                Logout();
                throw new AuthRequiredException();
            }

            Token token;
            try
            {
                // not cancellable: other callers may be waiting on the same refresh
                token = await _oauth().GetAccessTokenByRefreshTokenAsync(current.RefreshToken, CancellationToken.None);
            }
            catch (OAuthException e) when (e.Kind == OAuthErrorKind.InvalidTokenResponse && (e.StatusCode == 400 || e.StatusCode == 401))
            {
                // the refresh token was rotated elsewhere or the app was unlinked: only a new login helps
                Logout();
                throw new AuthRequiredException();
            }

            Save(new StoredToken
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                ExpiresAt = token.ExpiresAt,
                ScreenName = current.ScreenName,
                DisplayName = current.DisplayName
            });

            return token.AccessToken;
        }
    }

    internal static class TaskCancellationExtensions
    {
        /// <summary>Stops waiting when <paramref name="ct" /> is cancelled; the task itself keeps running.</summary>
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken ct)
        {
            if (!ct.CanBeCanceled || task.IsCompleted)
                return await task;

            var cancelled = new TaskCompletionSource<bool>();
            using (ct.Register(() => cancelled.TrySetResult(true)))
                if (await Task.WhenAny(task, cancelled.Task) != task)
                    throw new OperationCanceledException(ct);

            return await task;
        }
    }
}
