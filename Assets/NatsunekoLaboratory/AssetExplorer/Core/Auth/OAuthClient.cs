// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NatsunekoLaboratory.AssetExplorer.Json;

namespace NatsunekoLaboratory.AssetExplorer.Auth
{
    /// <summary>
    ///     OAuth 2.0 authorization code + PKCE for a public client.
    ///     Ported from the teyvat C# SDK (CatalystSharp.Auth.OAuthClient), which targets .NET 10 and cannot be used from Unity.
    ///     Differences: no client secret is ever sent (Petrichor for Unity is a public client), and the RFC 9207 <c>iss</c>
    ///     parameter is checked when present.
    /// </summary>
    internal sealed class OAuthClient
    {
        private readonly string _authorizeEndpoint;
        private readonly string _clientId;
        private readonly Func<DateTimeOffset> _clock;
        private readonly HttpClient _http;
        private readonly string _tokenEndpoint;

        public OAuthClient(string clientId, string authorizeEndpoint, string tokenEndpoint, HttpClient http, Func<DateTimeOffset> clock)
        {
            _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            _authorizeEndpoint = authorizeEndpoint ?? throw new ArgumentNullException(nameof(authorizeEndpoint));
            _tokenEndpoint = tokenEndpoint ?? throw new ArgumentNullException(nameof(tokenEndpoint));
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _clock = clock ?? (() => DateTimeOffset.UtcNow);

            if (!Uri.TryCreate(_authorizeEndpoint, UriKind.Absolute, out var authorize))
                throw new ArgumentException("authorizeEndpoint must be an absolute URL.", nameof(authorizeEndpoint));

            Issuer = authorize.GetLeftPart(UriPartial.Authority);
        }

        /// <summary>The authorization server's issuer: the authorize endpoint's origin.</summary>
        public string Issuer { get; }

        public Uri GetAuthorizeUrl(string redirectUri, Pkce pkce, string state)
        {
            var query = FormEncode(new[]
            {
                ("response_type", "code"),
                ("client_id", _clientId),
                ("redirect_uri", redirectUri),
                ("state", state),
                ("code_challenge", pkce.Challenge),
                ("code_challenge_method", pkce.Method)
            });

            return new Uri($"{new Uri(_authorizeEndpoint).GetLeftPart(UriPartial.Path)}?{query}");
        }

        /// <summary>Validates the callback URL and returns its authorization <c>code</c>.</summary>
        /// <param name="expectedIssuer">When not null, an <c>iss</c> parameter in the callback must equal it.</param>
        public string GetAuthorizationCode(Uri callbackUrl, string expectedState, string expectedRedirectUri, string expectedIssuer)
        {
            var text = callbackUrl.OriginalString;
            if (!text.StartsWith(expectedRedirectUri + "?", StringComparison.Ordinal))
                throw new OAuthException("Invalid authorization response", OAuthErrorKind.InvalidAuthorizationResponse);

            var query = ParseQuery(text);

            string First(string name)
            {
                return query.Where(w => w.Name == name).Select(w => w.Value).FirstOrDefault();
            }

            if (First("state") != expectedState)
                throw new OAuthException("Invalid authorization response", OAuthErrorKind.InvalidAuthorizationResponse);

            var error = First("error");
            if (error != null)
                throw new OAuthException("Authorization request failed", OAuthErrorKind.AuthorizationRequestFailed, error, First("error_description"));

            var iss = First("iss");
            if (iss != null && expectedIssuer != null && iss != expectedIssuer)
                throw new OAuthException("Invalid authorization response", OAuthErrorKind.InvalidAuthorizationResponse);

            return First("code") ?? throw new OAuthException("Invalid authorization response", OAuthErrorKind.InvalidAuthorizationResponse);
        }

        public Task<Token> GetAccessTokenByCodeAsync(string code, string redirectUri, Pkce pkce, CancellationToken ct)
        {
            return PostTokenAsync(new[]
            {
                ("grant_type", "authorization_code"),
                ("code", code),
                ("redirect_uri", redirectUri),
                ("client_id", _clientId),
                ("code_verifier", pkce.Verifier)
            }, ct);
        }

        public Task<Token> GetAccessTokenByRefreshTokenAsync(string refreshToken, CancellationToken ct)
        {
            return PostTokenAsync(new[]
            {
                ("grant_type", "refresh_token"),
                ("refresh_token", refreshToken),
                ("client_id", _clientId)
            }, ct);
        }

        private async Task<Token> PostTokenAsync((string Name, string Value)[] form, CancellationToken ct)
        {
            // the form is pure ASCII, so the content type carries no charset
            var content = new ByteArrayContent(Encoding.ASCII.GetBytes(FormEncode(form)));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            using (var request = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint) { Content = content })
            using (var response = await _http.SendAsync(request, ct))
            {
                var body = await response.Content.ReadAsStringAsync();
                object json = null;
                try
                {
                    json = JsonReader.Parse(body);
                }
                catch (JsonException)
                {
                    // handled below as an invalid response
                }

                if (!response.IsSuccessStatusCode)
                    throw new OAuthException("Invalid token response", OAuthErrorKind.InvalidTokenResponse, json.Str("error"), json.Str("error_description") ?? json.Str("message"), (int)response.StatusCode);

                var accessToken = json.Str("access_token");
                var refreshToken = json.Str("refresh_token");
                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
                    throw new OAuthException("Invalid token response", OAuthErrorKind.InvalidTokenResponse, statusCode: (int)response.StatusCode);

                return new Token
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    TokenType = json.Str("token_type"),
                    Scope = json.Str("scope"),
                    ExpiresAt = ParseExpiry(json)
                };
            }
        }

        private DateTimeOffset? ParseExpiry(object json)
        {
            var expiresAt = json.Str("expires_at");
            if (expiresAt != null && DateTimeOffset.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
                return at;

            var expiresIn = json.Long("expires_in");
            return expiresIn.HasValue ? _clock().AddSeconds(expiresIn.Value) : (DateTimeOffset?)null;
        }

        // application/x-www-form-urlencoded, matching URLSearchParams: only A-Z a-z 0-9 * - . _ stay as they are, space is "+".
        private static string FormEncode(IEnumerable<(string Name, string Value)> pairs)
        {
            return string.Join("&", pairs.Select(w => $"{FormEncodeComponent(w.Name)}={FormEncodeComponent(w.Value ?? "")}"));
        }

        private static string FormEncodeComponent(string value)
        {
            var sb = new StringBuilder();
            foreach (var b in Encoding.UTF8.GetBytes(value))
            {
                var c = (char)b;
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '*' || c == '-' || c == '.' || c == '_')
                    sb.Append(c);
                else if (c == ' ')
                    sb.Append('+');
                else
                    sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        /// <summary>The decoded <c>name=value</c> pairs of the query, in order (the fragment is ignored).</summary>
        private static List<(string Name, string Value)> ParseQuery(string url)
        {
            var hash = url.IndexOf('#');
            if (hash >= 0)
                url = url.Substring(0, hash);

            var question = url.IndexOf('?');
            if (question < 0)
                return new List<(string, string)>();

            return url.Substring(question + 1)
                      .Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(pair =>
                      {
                          var parts = pair.Split(new[] { '=' }, 2);
                          return (Decode(parts[0]), parts.Length > 1 ? Decode(parts[1]) : "");
                      })
                      .ToList();

            string Decode(string text)
            {
                return Uri.UnescapeDataString(text.Replace('+', ' '));
            }
        }
    }
}
