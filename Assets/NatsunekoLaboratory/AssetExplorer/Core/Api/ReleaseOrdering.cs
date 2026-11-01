// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace NatsunekoLaboratory.AssetExplorer.Api
{
    internal static class ReleaseOrdering
    {
        /// <summary>Verified first, then legacy, then unverified (matching the server's trust rank).</summary>
        public static int TrustRank(ReleaseStatus status)
        {
            switch (status)
            {
                case ReleaseStatus.Verified:
                    return 0;

                case ReleaseStatus.Legacy:
                    return 1;

                default:
                    return 2;
            }
        }

        public static List<ReleaseSummary> Sort(IEnumerable<ReleaseSummary> releases)
        {
            var list = releases.ToList();
            list.Sort((a, b) =>
            {
                var cmp = TrustRank(a.Status).CompareTo(TrustRank(b.Status));
                if (cmp != 0)
                    return cmp;

                cmp = VersionComparer.Compare(b.Version, a.Version);
                if (cmp != 0)
                    return cmp;

                // ISO 8601 timestamps from the server sort correctly as strings
                return string.CompareOrdinal(b.CreatedAt, a.CreatedAt);
            });

            return list;
        }
    }
}
