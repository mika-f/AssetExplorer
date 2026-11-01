// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests
{
    public class SmokeTests
    {
        [Test]
        public void Harness_runs()
        {
            Assert.That(1 + 1, Is.EqualTo(2));
        }
    }
}
