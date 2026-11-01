// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;

using NatsunekoLaboratory.AssetExplorer.Auth;

using UnityEditor;

namespace NatsunekoLaboratory.AssetExplorer.Integration
{
    /// <summary>
    ///     Endpoints and the OAuth client ID, stored per machine in EditorPrefs. An empty value means "use the default",
    ///     so staging can be tested by overriding a field and production comes back with "リセット".
    /// </summary>
    internal static class PetrichorSettings
    {
        public const string DefaultApiBaseUrl = "https://api.natsuneko.com";
        public const string DefaultWebBaseUrl = "https://petrichor.natsuneko.com";
        public const string DefaultAuthorizeUrl = "https://accounts.natsuneko.com/authorize";

        /// <summary>
        ///     The client ID of the Egeria application registered for this extension (a normal application, not a client-metadata
        ///     one, with redirect URI http://127.0.0.1:57193/callback). Fill it in before release; see README.
        /// </summary>
        public const string DefaultClientId = "";

        public const string KeyPrefix = "NatsunekoLaboratory.Petrichor.";

        public static string ApiBaseUrl
        {
            get => Get(nameof(ApiBaseUrl), DefaultApiBaseUrl).TrimEnd('/');
            set => Set(nameof(ApiBaseUrl), value);
        }

        public static string WebBaseUrl
        {
            get => Get(nameof(WebBaseUrl), DefaultWebBaseUrl).TrimEnd('/');
            set => Set(nameof(WebBaseUrl), value);
        }

        public static string AuthorizeUrl
        {
            get => Get(nameof(AuthorizeUrl), DefaultAuthorizeUrl);
            set => Set(nameof(AuthorizeUrl), value);
        }

        public static string ClientId
        {
            get => Get(nameof(ClientId), DefaultClientId);
            set => Set(nameof(ClientId), value);
        }

        public static string TokenEndpoint => ApiBaseUrl + "/token";

        public static string RedirectUri => LoopbackReceiver.DefaultRedirectUri;

        public static string ContributeUrl => WebBaseUrl + "/contribute";

        public static event Action Changed;

        public static string PackageUrl(string id)
        {
            return $"{WebBaseUrl}/packages/{Uri.EscapeDataString(id ?? "")}";
        }

        public static string ReleaseUrl(string id)
        {
            return $"{WebBaseUrl}/releases/{Uri.EscapeDataString(id ?? "")}";
        }

        public static string GuidUrl(string guid)
        {
            return $"{WebBaseUrl}/guid/{Uri.EscapeDataString(guid ?? "")}";
        }

        public static void Reset(string name)
        {
            if (!EditorPrefs.HasKey(KeyPrefix + name))
                return;

            EditorPrefs.DeleteKey(KeyPrefix + name);
            Changed?.Invoke();
        }

        public static void ResetAll()
        {
            foreach (var name in new[] { nameof(ApiBaseUrl), nameof(WebBaseUrl), nameof(AuthorizeUrl), nameof(ClientId) })
                EditorPrefs.DeleteKey(KeyPrefix + name);

            Changed?.Invoke();
        }

        private static string Get(string name, string fallback)
        {
            var value = EditorPrefs.GetString(KeyPrefix + name, "");
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static void Set(string name, string value)
        {
            value = value?.Trim() ?? "";
            if (EditorPrefs.GetString(KeyPrefix + name, "") == value)
                return;

            if (value.Length == 0)
                EditorPrefs.DeleteKey(KeyPrefix + name);
            else
                EditorPrefs.SetString(KeyPrefix + name, value);

            Changed?.Invoke();
        }
    }
}
