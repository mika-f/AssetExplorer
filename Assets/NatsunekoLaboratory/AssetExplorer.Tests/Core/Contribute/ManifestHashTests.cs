// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System.Linq;

using NatsunekoLaboratory.AssetExplorer.Contribute;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Contribute
{
    // Expected values were computed with teyvat's computeManifestId (packages/foreign-model/src/petrichor.ts).
    public class ManifestHashTests
    {
        [Test]
        public void Matches_server_for_ascii_paths_regardless_of_order()
        {
            var assets = new[]
            {
                new ManifestAsset(new string('b', 32), "Assets/B.png", null),
                new ManifestAsset(new string('a', 32), "Assets/A.mat", "NativeFormatImporter")
            };

            const string expected = "7a63a8f15e7b645b81f28e404d95d3da9e922ce9bb8b9278d8025301ac0d988e";
            Assert.That(ManifestHash.Compute(assets), Is.EqualTo(expected));
            Assert.That(ManifestHash.Compute(assets.Reverse()), Is.EqualTo(expected));
        }

        [Test]
        public void Matches_server_for_non_ascii_and_supplementary_paths()
        {
            var assets = new[]
            {
                new ManifestAsset("0123456789abcdef0123456789abcdef", "Assets/テクスチャ/𠮷.png", null),
                new ManifestAsset(new string('f', 32), "Assets/z.txt", null)
            };

            Assert.That(ManifestHash.Compute(assets), Is.EqualTo("a4178d5670e56ece444a0caedfbb682a952bd39bea8afbba02e69ab96310ff54"));
        }

        [Test]
        public void Matches_server_for_single_asset()
        {
            var assets = new[] { new ManifestAsset(new string('a', 32), "Assets/A.mat", null) };

            Assert.That(ManifestHash.Compute(assets), Is.EqualTo("9e710bb63a10f3025d1aea88f20955d849c5c312019caaa94aa52940c286d246"));
        }
    }
}
