// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using NatsunekoLaboratory.AssetExplorer.Scanning;

using NUnit.Framework;

namespace NatsunekoLaboratory.AssetExplorer.Tests.Scanning
{
    public class YamlGuidExtractorTests
    {
        private static readonly string A = "0123456789abcdef0123456789abcdef";
        private static readonly string B = new string('b', 32);

        [Test]
        public void Extracts_inline_reference_key()
        {
            var refs = YamlGuidExtractor.Extract($"MonoBehaviour:\n  m_Script: {{fileID: 11500000, guid: {A}, type: 3}}\n");

            Assert.That(refs, Is.EqualTo(new[] { (A, "m_Script") }));
        }

        [Test]
        public void Extracts_list_item_key()
        {
            var yaml = $"MeshRenderer:\n  m_Materials:\n  - {{fileID: 2100000, guid: {A}, type: 2}}\n  - {{fileID: 2100000, guid: {B}, type: 2}}\n";

            Assert.That(YamlGuidExtractor.Extract(yaml), Is.EqualTo(new[] { (A, "m_Materials"), (B, "m_Materials") }));
        }

        [Test]
        public void Extracts_key_on_list_item_mapping()
        {
            var yaml = $"PrefabInstance:\n  m_Modification:\n    m_Modifications:\n    - target: {{fileID: 1, guid: {A}, type: 3}}\n      propertyPath: m_Name\n";

            Assert.That(YamlGuidExtractor.Extract(yaml), Is.EqualTo(new[] { (A, "target") }));
        }

        [Test]
        public void Lowercases_guids_and_keeps_duplicates()
        {
            var yaml = $"  a: {{fileID: 1, guid: {A.ToUpperInvariant()}, type: 2}}\n  b: {{fileID: 1, guid: {A}, type: 2}}\n";

            Assert.That(YamlGuidExtractor.Extract(yaml), Is.EqualTo(new[] { (A, "a"), (A, "b") }));
        }

        [Test]
        public void Handles_crlf()
        {
            Assert.That(YamlGuidExtractor.Extract($"  m_Shader: {{fileID: 4800000, guid: {A}, type: 3}}\r\n"), Is.EqualTo(new[] { (A, "m_Shader") }));
        }

        [Test]
        public void IsYaml_detects_header()
        {
            Assert.That(YamlGuidExtractor.IsYaml("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"), Is.True);
            Assert.That(YamlGuidExtractor.IsYaml("\u0000\u0001binary"), Is.False);
            Assert.That(YamlGuidExtractor.IsYaml(null), Is.False);
        }
    }
}
