// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Linq;

using NatsunekoLaboratory.AssetExplorer.Api;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Api
{
    public class ReleaseOrderingTests
    {
        private static ReleaseSummary R(string id, ReleaseStatus status, string version, string createdAt = "2026-01-01T00:00:00.000Z")
        {
            return new ReleaseSummary { Id = id, Status = status, Version = version, CreatedAt = createdAt };
        }

        [Test]
        public void Sort_puts_verified_then_legacy_then_unverified()
        {
            var sorted = ReleaseOrdering.Sort(new[] { R("u", ReleaseStatus.Unverified, "9"), R("l", ReleaseStatus.Legacy, "1"), R("v", ReleaseStatus.Verified, "1") });

            Assert.That(sorted.Select(w => w.Id), Is.EqualTo(new[] { "v", "l", "u" }));
        }

        [Test]
        public void Sort_orders_versions_naturally()
        {
            var sorted = ReleaseOrdering.Sort(new[] { R("a", ReleaseStatus.Verified, "1.9.0"), R("b", ReleaseStatus.Verified, "1.10.0") });

            Assert.That(sorted.Select(w => w.Id), Is.EqualTo(new[] { "b", "a" }));
        }

        [Test]
        public void Sort_breaks_ties_by_newest()
        {
            var sorted = ReleaseOrdering.Sort(new[] { R("old", ReleaseStatus.Verified, "1.0", "2025-01-01T00:00:00.000Z"), R("new", ReleaseStatus.Verified, "1.0", "2026-01-01T00:00:00.000Z") });

            Assert.That(sorted.Select(w => w.Id), Is.EqualTo(new[] { "new", "old" }));
        }

        [Test]
        public void VersionComparer_compares_numeric_runs_as_numbers()
        {
            Assert.That(VersionComparer.Compare("1.10.0", "1.9.0"), Is.GreaterThan(0));
            Assert.That(VersionComparer.Compare("v2", "v10"), Is.LessThan(0));
            Assert.That(VersionComparer.Compare("1.0.0", "1.0.0"), Is.EqualTo(0));
            Assert.That(VersionComparer.Compare("1.0.0-beta", "1.0.0"), Is.GreaterThan(0));
            Assert.That(VersionComparer.Compare(null, "1"), Is.LessThan(0));
        }
    }
}
