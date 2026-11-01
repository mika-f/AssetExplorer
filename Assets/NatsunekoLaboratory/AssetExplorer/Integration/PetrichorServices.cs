// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Net.Http;

using NatsunekoLaboratory.AssetExplorer.Api;
using NatsunekoLaboratory.AssetExplorer.Auth;

using UnityEngine;

namespace NatsunekoLaboratory.AssetExplorer.Integration
{
    /// <summary>The shared HTTP client, login session and API client used by every tab.</summary>
    internal static class PetrichorServices
    {
        public const string Version = "2.0.0";

        private static HttpClient _http;
        private static AuthSession _auth;
        private static PetrichorClient _client;

        public static HttpClient Http => _http ?? (_http = new HttpClient { Timeout = TimeSpan.FromSeconds(100) });

        public static AuthSession Auth
        {
            get
            {
                Ensure();
                return _auth;
            }
        }

        public static PetrichorClient Client
        {
            get
            {
                Ensure();
                return _client;
            }
        }

        public static bool HasClientId => !string.IsNullOrWhiteSpace(PetrichorSettings.ClientId);

        private static void Ensure()
        {
            if (_client != null)
                return;

            PetrichorClient.UserAgent = $"Petrichor-Unity/{Version} (Unity {Application.unityVersion})";

            // OAuthClient is built per use so endpoint and client ID changes in Settings apply immediately
            _auth = new AuthSession(new EditorPrefsTokenStorage(),
                                    () => new OAuthClient(PetrichorSettings.ClientId, PetrichorSettings.AuthorizeUrl, PetrichorSettings.TokenEndpoint, Http, () => DateTimeOffset.UtcNow),
                                    () => DateTimeOffset.UtcNow);
            _client = new PetrichorClient(Http, () => PetrichorSettings.ApiBaseUrl, _auth);

            // the stored login is per API base URL
            PetrichorSettings.Changed += _auth.Reload;
        }
    }
}
