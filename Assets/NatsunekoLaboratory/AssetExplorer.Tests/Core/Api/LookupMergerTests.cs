// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

using NatsunekoLaboratory.AssetExplorer.Api;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Api
{
    public class LookupMergerTests
    {
        private static string Guid(int i)
        {
            return i.ToString("x32");
        }

        private static Suggestion S(string id, ReleaseStatus status, int matched, string version = "1.0")
        {
            return new Suggestion { Release = new ReleaseSummary { Id = id, Status = status, Version = version }, Matched = matched };
        }

        [Test]
        public void Split_normalizes_and_dedupes()
        {
            var upper = " " + new string('A', 32) + " ";
            var batches = LookupMerger.Split(new[] { upper, new string('a', 32) });

            Assert.That(batches.Count, Is.EqualTo(1));
            Assert.That(batches[0], Is.EqualTo(new[] { new string('a', 32) }));
        }

        [Test]
        public void Split_batches_by_1000()
        {
            var batches = LookupMerger.Split(Enumerable.Range(0, 2001).Select(Guid));

            Assert.That(batches.Select(w => w.Count), Is.EqualTo(new[] { 1000, 1000, 1 }));
            Assert.That(batches[0][0], Is.EqualTo(Guid(0)));
        }

        [Test]
        public void Split_skips_blank_entries()
        {
            Assert.That(LookupMerger.Split(new[] { "", "  ", null }), Is.Empty);
        }

        [Test]
        public void Merge_unions_results_and_missing()
        {
            var a = new BulkLookup { Results = { { "g1", new AssetLookup { Guid = "g1" } } }, Missing = { "m1" } };
            var b = new BulkLookup { Results = { { "g2", new AssetLookup { Guid = "g2" } } }, Missing = { "m2" } };

            var merged = LookupMerger.Merge(new[] { a, b });

            Assert.That(merged.Results.Keys, Is.EquivalentTo(new[] { "g1", "g2" }));
            Assert.That(merged.Missing, Is.EqualTo(new[] { "m1", "m2" }));
        }

        [Test]
        public void Merge_sums_matched_for_same_release()
        {
            var a = new BulkLookup { Suggestions = { S("r1", ReleaseStatus.Verified, 3) } };
            var b = new BulkLookup { Suggestions = { S("r1", ReleaseStatus.Verified, 2) } };

            var merged = LookupMerger.Merge(new[] { a, b });

            Assert.That(merged.Suggestions.Single().Matched, Is.EqualTo(5));
        }

        [Test]
        public void Merge_sorts_by_trust_then_matched()
        {
            var part = new BulkLookup { Suggestions = { S("u", ReleaseStatus.Unverified, 9), S("v1", ReleaseStatus.Verified, 1), S("v5", ReleaseStatus.Verified, 5), S("l", ReleaseStatus.Legacy, 2) } };

            var merged = LookupMerger.Merge(new[] { part });

            Assert.That(merged.Suggestions.Select(w => w.Release.Id), Is.EqualTo(new[] { "v5", "v1", "l", "u" }));
        }

        [Test]
        public void Merge_cuts_to_20()
        {
            var part = new BulkLookup { Suggestions = Enumerable.Range(0, 30).Select(i => S($"r{i}", ReleaseStatus.Verified, i)).ToList() };

            Assert.That(LookupMerger.Merge(new List<BulkLookup> { part }).Suggestions.Count, Is.EqualTo(20));
        }
    }
}
