// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;

using NatsunekoLaboratory.AssetExplorer.Json;

namespace NatsunekoLaboratory.AssetExplorer.Auth
{
    /// <summary>The persisted login: tokens plus the account names shown in the window.</summary>
    internal sealed class StoredToken
    {
        public string AccessToken { get; set; }

        public string RefreshToken { get; set; }

        public DateTimeOffset? ExpiresAt { get; set; }

        public string ScreenName { get; set; }

        public string DisplayName { get; set; }

        public string ToJson()
        {
            return JsonWriter.Write(new Dictionary<string, object>
            {
                { "accessToken", AccessToken },
                { "refreshToken", RefreshToken },
                { "expiresAt", ExpiresAt?.ToString("o", CultureInfo.InvariantCulture) },
                { "screenName", ScreenName },
                { "displayName", DisplayName }
            });
        }

        public static StoredToken FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            object root;
            try
            {
                root = JsonReader.Parse(json);
            }
            catch (JsonException)
            {
                return null;
            }

            var expiresAt = root.Str("expiresAt");
            return new StoredToken
            {
                AccessToken = root.Str("accessToken"),
                RefreshToken = root.Str("refreshToken"),
                ExpiresAt = expiresAt != null && DateTimeOffset.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : (DateTimeOffset?)null,
                ScreenName = root.Str("screenName"),
                DisplayName = root.Str("displayName")
            };
        }
    }
}
