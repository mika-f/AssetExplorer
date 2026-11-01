// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace NatsunekoLaboratory.AssetExplorer.Api
{
    /// <summary>
    ///     The bulk lookup accepts at most 1,000 GUIDs per call. Larger sets are split here and the responses merged back.
    /// </summary>
    internal static class LookupMerger
    {
        public const int BatchSize = 1000;

        public const int MaxSuggestions = 20;

        public static List<List<string>> Split(IEnumerable<string> guids)
        {
            var seen = new HashSet<string>();
            var batches = new List<List<string>>();

            foreach (var raw in guids)
            {
                var guid = raw?.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(guid) || !seen.Add(guid))
                    continue;

                if (batches.Count == 0 || batches[batches.Count - 1].Count == BatchSize)
                    batches.Add(new List<string>());

                batches[batches.Count - 1].Add(guid);
            }

            return batches;
        }

        public static BulkLookup Merge(IEnumerable<BulkLookup> parts)
        {
            var merged = new BulkLookup();
            var suggestions = new Dictionary<string, Suggestion>();

            foreach (var part in parts)
            {
                foreach (var result in part.Results)
                    merged.Results[result.Key] = result.Value;

                merged.Missing.AddRange(part.Missing);

                // each batch has distinct GUIDs, so matched counts for the same release add up
                foreach (var suggestion in part.Suggestions)
                    if (suggestions.TryGetValue(suggestion.Release.Id, out var existing))
                        existing.Matched += suggestion.Matched;
                    else
                        suggestions[suggestion.Release.Id] = new Suggestion { Release = suggestion.Release, Matched = suggestion.Matched };
            }

            merged.Suggestions = suggestions.Values
                                            .OrderBy(w => ReleaseOrdering.TrustRank(w.Release.Status))
                                            .ThenByDescending(w => w.Matched)
                                            .ThenByDescending(w => w.Release.Version, Comparer<string>.Create(VersionComparer.Compare))
                                            .Take(MaxSuggestions)
                                            .ToList();

            return merged;
        }
    }
}
