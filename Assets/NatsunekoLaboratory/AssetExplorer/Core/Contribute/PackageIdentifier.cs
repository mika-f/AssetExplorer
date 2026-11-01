// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Text.RegularExpressions;

namespace NatsunekoLaboratory.AssetExplorer.Contribute
{
    /// <summary>
    ///     Same rules as teyvat's <c>identifierFromUrl</c>: a BOOTH item page becomes <c>booth:&lt;item id&gt;</c>, anything else stays a URL.
    /// </summary>
    internal static class PackageIdentifier
    {
        private static readonly Regex BoothItemPath = new Regex(@"^(?:/[a-z]{2}(?:-[a-z]+)?)?/items/(\d+)/?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool TryParseBooth(string url, out string itemId)
        {
            itemId = null;
            if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri))
                return false;

            var host = uri.Host.ToLowerInvariant();
            if (host != "booth.pm" && !host.EndsWith(".booth.pm", StringComparison.Ordinal))
                return false;

            var match = BoothItemPath.Match(uri.AbsolutePath);
            if (!match.Success)
                return false;

            itemId = match.Groups[1].Value;
            return true;
        }

        public static (IdentifierProvider Provider, string Value) FromUrl(string url)
        {
            return TryParseBooth(url, out var itemId) ? (IdentifierProvider.Booth, itemId) : (IdentifierProvider.Url, url);
        }
    }
}
