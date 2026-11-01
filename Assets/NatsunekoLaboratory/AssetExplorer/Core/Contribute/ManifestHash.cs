// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NatsunekoLaboratory.AssetExplorer.Contribute
{
    /// <summary>
    ///     The manifest ID, computed exactly like the server: sha256 hex of <c>${guid}\t${path}\n</c> lines,
    ///     sorted by the UTF-8 bytes of the GUID, then of the path.
    /// </summary>
    internal static class ManifestHash
    {
        public static string Compute(IEnumerable<ManifestAsset> assets)
        {
            var utf8 = new UTF8Encoding(false);
            var entries = assets.Select(w => new { w.Guid, w.Path, GuidBytes = utf8.GetBytes(w.Guid), PathBytes = utf8.GetBytes(w.Path) }).ToList();
            entries.Sort((a, b) =>
            {
                var cmp = CompareBytes(a.GuidBytes, b.GuidBytes);
                return cmp != 0 ? cmp : CompareBytes(a.PathBytes, b.PathBytes);
            });

            var sb = new StringBuilder();
            foreach (var entry in entries)
                sb.Append(entry.Guid).Append('\t').Append(entry.Path).Append('\n');

            using (var sha256 = SHA256.Create())
            {
                var digest = sha256.ComputeHash(utf8.GetBytes(sb.ToString()));
                return string.Concat(digest.Select(w => w.ToString("x2")));
            }
        }

        private static int CompareBytes(byte[] x, byte[] y)
        {
            var length = x.Length < y.Length ? x.Length : y.Length;
            for (var i = 0; i < length; i++)
                if (x[i] != y[i])
                    return x[i] - y[i];

            return x.Length - y.Length;
        }
    }
}
